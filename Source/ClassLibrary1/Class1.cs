using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;
using static System.Collections.Specialized.BitVector32;

namespace LibraryOfTheRim
{

    [StaticConstructorOnStartup]
    public static class PawnKindClassifier
    {
        static PawnKindClassifier()
        {
            LongEventHandler.ExecuteWhenFinished(Init);
        }

        public static Dictionary<int, List<PawnKindDef>> rankDictionary = new Dictionary<int, List<PawnKindDef>>();

        private static void Init()
        {
            Log.Message("[Library of The Rim] Started reclassification of pawnkinds");


            // commented bc this system sucked ass. might re-add later as an option

            // old system: actually try to calculate the values and shove em into ranks based on thresholds. probably more lore accurate but sucks ass for balancing
            
            //foreach (PawnKindDef pk in DefDatabase<PawnKindDef>.AllDefs)
            //{
            //if (pk.race != null && pk.race.race != null && !pk.race.race.Humanlike)
            //    continue;

            //TechLevel tech =
            //    pk.defaultFactionDef?.techLevel ?? TechLevel.Neolithic;
            //int techTier = 1;
            //switch(tech){
            //    case TechLevel.Animal:
            //        techTier = 1;
            //        break;
            //    case TechLevel.Neolithic:
            //        techTier = 2;
            //        break;
            //    case TechLevel.Medieval:
            //        techTier = 3;
            //        break;
            //    case TechLevel.Industrial:
            //        techTier = 4;
            //        break;
            //    case TechLevel.Spacer:
            //        techTier = 5;
            //        break;
            //    case TechLevel.Ultra:
            //        techTier = 6;
            //        break;
            //    case TechLevel.Archotech:
            //        techTier = 7;
            //        break;
            //}
            //float score =
            //    pk.combatPower +
            //    pk.weaponMoney.max * 0.2f +
            //    pk.apparelMoney.max * 0.2f;

            //score *= (1f + techTier * 0.25f);

            //float normalized = score / 100f;

            //float scaled = Mathf.Pow(normalized, 0.6f) * 10f;

            //int rank =
            //    scaled < 5 ? 1 :
            //    scaled < 15 ? 2 :
            //    scaled < 30 ? 3 :
            //    scaled < 50 ? 4 :
            //    scaled < 80 ? 5 :
            //    scaled < 120 ? 6 : 7;

            //    Log.Message($"KindDef: {pk.label} ,Score : {score}, Rank : {rank}");

            //    if (!rankDictionary.ContainsKey(rank))
            //    {
            //        rankDictionary[rank] = new List<PawnKindDef>();
            //    }

            //    rankDictionary[rank].Add(pk);
            //}


            // new system: shove all pawns into a list, divide the list by 7 and call it a day

            var list = DefDatabase<PawnKindDef>.AllDefs
            .Where(pk => pk.race?.race?.Humanlike == true)
            .Select(pk => new
            {
                pk,
                score =
                    pk.combatPower +
                    pk.weaponMoney.max * 0.2f +
                    pk.apparelMoney.max * 0.2f
            })
            .ToList();

            list = list.OrderBy(x => x.score).ToList();

            for (int i = 0; i < list.Count; i++)
            {
                int rank = (i * 7) / list.Count + 1;

                var pk = list[i].pk;

                if (!rankDictionary.ContainsKey(rank))
                    rankDictionary[rank] = new List<PawnKindDef>();

                rankDictionary[rank].Add(pk);
            }

            Log.Message($"[Library of The Rim] Pawn kinds reclassification complete.");
        }
    }


    // Library book related bs

    public class CompProperties_LibraryBook : CompProperties
    {
        public int minRank = 1;
        public int maxRank = 1;
        public CompProperties_LibraryBook()
        {
            compClass = typeof(CompLibBook);
        }
    }

    public class CompLibBook : ThingComp
    {

        public PawnKindDef pawnKind;

        public CompProperties_LibraryBook Props => (CompProperties_LibraryBook)props;
        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Defs.Look(ref pawnKind, "pawnKind");
        }
        public override void PostPostMake()
        {
            base.PostPostMake();

            pawnKind = getRandomPawn();
        }

        public PawnKindDef getRandomPawn()
        {
            int rank = Rand.RangeInclusive(Props.minRank, Props.maxRank);

            if (!PawnKindClassifier.rankDictionary.TryGetValue(rank, out var list) ||
            list.NullOrEmpty())
            {
                Log.Warning("Book generation yielded no pawnkind. Perhaps there was an error during pawn filtration on startup");
                return null;

            }

            return list.RandomElement();
        }

        public override string CompInspectStringExtra()
        {
            string text = "book";
            if (pawnKind == null)
            {
                text = "Book of no one in particular";
            }
            else
            {
                bool isVowel = "aeiouAEIOU".IndexOf(text[0]) >= 0;
                if (isVowel)
                {
                    text = $"Book of an {pawnKind.label}";
                }
                else
                {
                    text = $"Book of a {pawnKind.label}";
                }

                if (Prefs.DevMode)
                {
                    text += $"\nDEBUG: defName = {pawnKind.defName}";
                }
            }
            return text;
        }


    }

    // Book burning job

    public class JobDriver_BurnBook : JobDriver
    {
        public void RemoveBiocoding(Thing item)
        {
            if (item is ThingWithComps thingWithComps)
            {
                CompBiocodable biocodeComp = thingWithComps.GetComp<CompBiocodable>();

                if (biocodeComp != null)
                {
                    thingWithComps.AllComps.Remove(biocodeComp);
                    Log.Message($"[Library of The Rim] Removed biocoding on {item}");
                }
            }
        }
        public int GetAmount()
        {
            Dictionary<int, float> randomAmount = new Dictionary<int, float>
            {
                { 1, 30f },
                { 2, 15f },
                { 3, 7f },
                { 4, 3f }
            };
            Log.Message($"[Library of The Rim] Generating a random amount of loot");
            return randomAmount.RandomElementByWeight(kvp => kvp.Value).Key;
        }
        private Thing Book => job.targetA.Thing;
        SoundDef bookOnSound = SoundDef.Named("book_on");
        SoundDef bookPopSound = SoundDef.Named("book_pop");
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Book, job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil read = Toils_General.Wait(300);
            read.WithProgressBarToilDelay(TargetIndex.A);
            read.initAction = () =>
            {
                if (bookOnSound != null && Book.Map != null)
                {
                    bookOnSound.PlayOneShot(SoundInfo.InMap(Book));
                }
            };
            yield return read;
            Toil finish = new Toil();
            finish.initAction = () =>
            {
                CompLibBook comp = Book.TryGetComp<CompLibBook>();
                if (comp != null)
                {
                    Map currentMap = comp.parent.Map;
                    if (currentMap == null)
                    {
                        return;
                    }
                    List<Thing> generatedLoot = new List<Thing>();
                    List<Thing> finalLoot = new List<Thing>();


                    PawnGenerationRequest request = new PawnGenerationRequest(
                            kind: comp.pawnKind,
                            faction: null,
                            context: PawnGenerationContext.NonPlayer,
                            forceGenerateNewPawn: true,
                            canGeneratePawnRelations: false,
                            allowDead: false,
                            allowDowned: false,
                            mustBeCapableOfViolence: false,
                            allowGay: false,
                            allowPregnant: false,
                            allowAddictions: false
                        );
                    Pawn dummyPawn = PawnGenerator.GeneratePawn(request);
                    Log.Message($"[Library of The Rim] Dummypawn generated");
                    if (dummyPawn.equipment != null)
                    {
                        foreach (ThingWithComps eq in dummyPawn.equipment.AllEquipmentListForReading.ToList())
                        {
                            dummyPawn.equipment.Remove(eq);
                            RemoveBiocoding(eq);
                            generatedLoot.Add(eq);
                        }
                    }
                    if (dummyPawn.apparel != null)
                    {
                        foreach (Apparel ap in dummyPawn.apparel.WornApparel.ToList())
                        {
                            dummyPawn.apparel.Remove(ap);
                            generatedLoot.Add(ap);
                        }
                    }
                    if (dummyPawn.health?.hediffSet?.hediffs != null)
                    {
                        foreach (Hediff hediff in dummyPawn.health.hediffSet.hediffs.ToList())
                        {
                            if (hediff.def.spawnThingOnRemoved != null)
                            {
                                ThingDef prostheticDef = hediff.def.spawnThingOnRemoved;

                                Thing prostheticItem = ThingMaker.MakeThing(prostheticDef);
                                {
                                    generatedLoot.Add(prostheticItem);
                                }
                            }
                        }
                    }
                    Log.Message($"[Library of The Rim] Dummypawn equipment pooled");
                    
                    // commented bc idk if i should keep it.

                    //if (dummyPawn.inventory != null)
                    //{
                    //    foreach (ThingWithComps inv in dummyPawn.inventory.innerContainer.ToList())
                    //    {
                    //        dummyPawn.inventory.innerContainer.Remove(inv);
                    //        generatedLoot.Add(inv);
                    //    }
                    //}

                    dummyPawn.Destroy(DestroyMode.Vanish);

                    if (Find.WorldPawns.Contains(dummyPawn))
                    {
                        Find.WorldPawns.RemovePawn(dummyPawn);
                    }
                    Log.Message($"[Library of The Rim] Dummypawn destroyed");
                    Thing randomItem = null;
                    int v = GetAmount();
                    for (int i = v; i >= 0; i--)
                    {
                        randomItem = generatedLoot.RandomElement();
                        if (randomItem == null)
                        {
                            continue;
                        }
                        Log.Message($"[Library of The Rim] Added {randomItem} to the final loot pool");
                        generatedLoot.Remove(randomItem);
                        finalLoot.Add(randomItem);

                    }
                    ;

                    foreach (Thing item in finalLoot)
                    {
                        Log.Message($"[Library of The Rim] placing {item} on {Book.Position}");
                        GenPlace.TryPlaceThing(item, Book.Position, currentMap, ThingPlaceMode.Near);
                    }
                    if (bookPopSound != null && Book.Map != null)
                    {
                        bookPopSound.PlayOneShot(SoundInfo.InMap(Book));
                    }
                    Book.Destroy(DestroyMode.Vanish);
                }

            };

            yield return finish;

        }
    }

    // Invitation event worker

    public class InvitationWorker : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map currentMap = (Map)parms.target;
            if (currentMap == null)
            {
                return false;
            }
            ;

            if (currentMap.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                return false;
            }

            return base.CanFireNowSub(parms);

        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map currentMap = (Map)parms.target;
            List<Pawn> pawns = currentMap.mapPawns.FreeColonistsSpawned;
            if (pawns.Empty())
            {
                return false;
            }
            Pawn targetPawn = pawns.RandomElement();

            Thing thing = ThingMaker.MakeThing(ThingDef.Named("library_invitation"));
            if (targetPawn.inventory?.innerContainer != null)
            {
                targetPawn.inventory.innerContainer.TryAdd(thing);
            }
            String pronoun = targetPawn.gender.GetPronoun();
            String pronounPos = targetPawn.gender.GetPossessive();
            SendIncidentLetter(
                "Invitation received",
                $"{targetPawn.LabelShortCap} has found a weird invitation in {pronounPos} inventory. {pronoun} has no memory of it being there. \n\n\n\"Dear Guest: I formally invite you to the library.\r\nThe Library's books can provide you with all the wisdom, wealth, honor, and power you seek.\r\nHowever, an ordeal will await you in the library.\r\nIf you cannot overcome this ordeal, you will be converted into a book yourself.\"\n",
                LetterDefOf.NeutralEvent,
                parms,
                new LookTargets(targetPawn),
                def,
                targetPawn.Named("PAWN")
            );
            return true;
        }
    }
    // Invitation sign job driver
    public class JobDriver_SignLibraryInvitation : JobDriver
    {
        private Thing Invitation => job.targetA.Thing;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Invitation, job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil sign = Toils_General.Wait(500);
            sign.WithProgressBarToilDelay(TargetIndex.A);
            yield return sign;
            Toil finish = new Toil();

            Building libraryDoor = (Building)ThingMaker.MakeThing(ThingDef.Named("LibraryEntrance"));
            finish.initAction = () =>
            {
                GenSpawn.Spawn(
                        libraryDoor,
                        Invitation.Position,
                        Invitation.Map,
                        WipeMode.Vanish
                    );
                if (!Invitation.DestroyedOrNull())
                {
                    Invitation.Destroy();
                }
            };
            yield return finish;
        }
    }

    // Library entrance/exit map portal

    public class LibraryEntrance : MapPortal
    {
        public override void OnEntered(Pawn pawn)
        {
            if (!beenEntered)
            {
                TaggedString text = "EnteredMegahiveText".Translate(pawn.Named("PAWN"));
            }
            base.OnEntered(pawn);
        }
    }
    
    // Genstep for library generation. later make generate specific floors once i add em

    public class GenStep_test : GenStep
    {
        public override int SeedPart => 12412314;

        private PocketMapExit exit;

        public override void Generate(Map map, GenStepParams parms)
        {   

            // debug - set all floor tiles to concrete

            foreach (IntVec3 c in map.AllCells)
            {
                map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
                
            }
            // debug - list of all spawned things
            List<Thing> spawned = new List<Thing>();

            // prefab placement
            IntVec3 pos = map.Center;

            PrefabDef prefab = DefDatabase<PrefabDef>.GetNamed("Library_testFloor");

            Log.Message($"[Library of The Rim] Spawning {prefab} at map {map}, positioned at {pos}");
            PrefabUtility.SpawnPrefab(
                prefab,
                map,
                pos,
                Rot4.North,
                // debug, add the spawned doodad to list
                spawned: spawned
            );

            // set the player start position since the game needs it to properly fog
            exit = map.listerThings.ThingsOfDef(ThingDef.Named("LibraryExit")).FirstOrDefault() as PocketMapExit;
            if (exit == null)
            {
                Log.Error("[Library of The Rim] No exit found for library generation.");
            }
            Log.Message($"[Library of The Rim] Exit found! {exit} at {exit.Position}");
            MapGenerator.PlayerStartSpot = exit.Position;


            Log.Message($"[Library of The Rim] Spawned {spawned.Count} things.");

            // jesus christ this sucks
        }
    }
}