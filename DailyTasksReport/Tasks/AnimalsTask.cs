﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DailyTasksReport.UI;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace DailyTasksReport.Tasks
{
    public class AnimalsTask : Task
    {
        private static readonly int[] CollectableAnimalProducts = {107, 174, 176, 180, 182, 440, 442, 444, 446};

        private static readonly List<TaskItem<Object>> AnimalProductsToCollect = new List<TaskItem<Object>>();
        private static readonly List<TaskItem<Object>> TrufflesToCollect = new List<TaskItem<Object>>();
        private static readonly List<TaskItem<FarmAnimal>> AnimalProductsToHarvest = new List<TaskItem<FarmAnimal>>();
        private static readonly List<TaskItem<FarmAnimal>> UnpettedAnimals = new List<TaskItem<FarmAnimal>>();
        private static readonly List<Tuple<Building, int>> MissingHay = new List<Tuple<Building, int>>();

        private readonly ModConfig _config;
        private readonly AnimalsTaskId _id;
        private static Farm _farm;

        internal AnimalsTask(ModConfig config, AnimalsTaskId id)
        {
            _config = config;
            _id = id;

            if (ObjectsNames.Count == 0)
                PopulateObjectsNames();

            SettingsMenu.ReportConfigChanged += SettingsMenu_ReportConfigChanged;

            if (id == AnimalsTaskId.UnpettedAnimals)
            {
                MenuEvents.MenuChanged += MenuEvents_MenuChanged;
                MenuEvents.MenuClosed += MenuEvents_MenuClosed;
            }
        }

        private void SettingsMenu_ReportConfigChanged(object sender, SettingsChangedEventArgs e)
        {
            switch (_id)
            {
                case AnimalsTaskId.UnpettedAnimals:
                    Enabled = _config.UnpettedAnimals;
                    break;
                case AnimalsTaskId.AnimalProducts:
                    Enabled = _config.AnimalProducts.ContainsValue(true);
                    break;
                case AnimalsTaskId.MissingHay:
                    Enabled = _config.MissingHay;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown animal task");
            }
        }

        private static void MenuEvents_MenuClosed(object sender, EventArgsClickableMenuClosed e)
        {
            if (e.PriorMenu is PurchaseAnimalsMenu ||
                e.PriorMenu is NamingMenu ||
                e.PriorMenu.GetType().FullName == "FarmExpansion.Menus.FEPurchaseAnimalsMenu")
                ReScanUnpettedAnimals();
        }

        private static void MenuEvents_MenuChanged(object sender, EventArgsClickableMenuChanged e)
        {
            if (e.PriorMenu is PurchaseAnimalsMenu ||
                e.PriorMenu is NamingMenu ||
                e.PriorMenu?.GetType().FullName == "FarmExpansion.Menus.FEPurchaseAnimalsMenu")
                ReScanUnpettedAnimals();
        }

        private static void ReScanUnpettedAnimals()
        {
            UnpettedAnimals.Clear();

            foreach (var animal in _farm.animals)
                if (!animal.Value.wasPet)
                    UnpettedAnimals.Add(new TaskItem<FarmAnimal>(_farm, animal.Value.Position, animal.Value));

            foreach (var building in _farm.buildings)
            {
                if (building.isUnderConstruction() || !(building.indoors is AnimalHouse animalHouse))
                    continue;

                foreach (var animal in animalHouse.animals)
                    if (!animal.Value.wasPet)
                        UnpettedAnimals.Add(new TaskItem<FarmAnimal>(animalHouse, animal.Value.Position, animal.Value));
            }
        }

        public override void FirstScan()
        {
            if (UnpettedAnimals.Count + AnimalProductsToCollect.Count + TrufflesToCollect.Count +
                AnimalProductsToHarvest.Count + MissingHay.Count > 0)
                return;

            _farm = Game1.locations.Find(l => l is Farm) as Farm;

            // Checking animals left outside
            CheckAnimals(_farm);

            // ReSharper disable once PossibleNullReferenceException
            foreach (var building in _farm.buildings)
            {
                if (building.isUnderConstruction()) continue;

                switch (building.indoors)
                {
                    case AnimalHouse animalHouse:
                        // Check animals
                        CheckAnimals(animalHouse);

                        // Check for object in Coop
                        if (building is Coop)
                            CheckAnimalProductsInCoop(building.indoors);

                        // Check for hay
                        var count = animalHouse.numberOfObjectsWithName("Hay");
                        if (count < animalHouse.animalLimit)
                            MissingHay.Add(new Tuple<Building, int>(building, animalHouse.animalLimit - count));
                        break;

                    case SlimeHutch slimeHutch:
                        // Check slime balls
                        foreach (var pair in building.indoors.objects)
                            if (pair.Value.parentSheetIndex >= 56 && pair.Value.parentSheetIndex <= 61)
                                AnimalProductsToCollect.Add(new TaskItem<Object>(slimeHutch, pair.Key, pair.Value));
                        break;
                }
            }

            CheckForTruffles(_farm);
        }

        private void UpdateList()
        {
            switch (_id)
            {
                case AnimalsTaskId.UnpettedAnimals:
                    UnpettedAnimals.RemoveAll(a => a.Object.wasPet || a.Object.health <= 0);
                    foreach (var animal in UnpettedAnimals)
                        animal.Location = _farm.animals.ContainsKey(animal.Object.myID)
                            ? _farm
                            : animal.Object.home.indoors;
                    break;

                case AnimalsTaskId.AnimalProducts:
                    AnimalProductsToCollect.RemoveAll(i =>
                        !(i.Location.objects.TryGetValue(i.Position, out var obj) &&
                          obj.parentSheetIndex == i.Object.parentSheetIndex));
                    TrufflesToCollect.Clear();
                    CheckForTruffles(_farm);
                    AnimalProductsToHarvest.RemoveAll(animal =>
                        animal.Object.currentProduce <= 0 || animal.Object.health <= 0);
                    foreach (var animal in AnimalProductsToHarvest)
                        animal.Location = _farm.animals.ContainsKey(animal.Object.myID)
                            ? _farm
                            : animal.Object.home.indoors;
                    break;

                case AnimalsTaskId.MissingHay:
                    MissingHay.Clear();
                    foreach (var building in _farm.buildings)
                    {
                        if (building.isUnderConstruction() || !(building.indoors is AnimalHouse animalHouse)) continue;
                        var count = animalHouse.numberOfObjectsWithName("Hay");
                        if (count < animalHouse.animalLimit)
                            MissingHay.Add(new Tuple<Building, int>(building, animalHouse.animalLimit - count));
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown animal task");
            }
        }

        private static void CheckAnimals(Farm farm)
        {
            foreach (var animal in farm.animals)
            {
                if (!animal.Value.wasPet)
                    UnpettedAnimals.Add(new TaskItem<FarmAnimal>(farm, animal.Value.Position, animal.Value));

                if (animal.Value.currentProduce > 0 && animal.Value.currentProduce != 430)
                    AnimalProductsToHarvest.Add(new TaskItem<FarmAnimal>(farm, animal.Value.Position, animal.Value));
            }
        }

        private static void CheckAnimals(AnimalHouse location)
        {
            foreach (var animal in location.animals)
            {
                if (!animal.Value.wasPet)
                    UnpettedAnimals.Add(new TaskItem<FarmAnimal>(location, animal.Value.position, animal.Value));

                if (animal.Value.currentProduce > 0 && animal.Value.currentProduce != 430)
                    AnimalProductsToHarvest.Add(new TaskItem<FarmAnimal>(location, animal.Value.position,
                        animal.Value));
            }
        }

        private static void CheckAnimalProductsInCoop(GameLocation coop)
        {
            foreach (var pair in coop.objects)
                if (Array.BinarySearch(CollectableAnimalProducts, pair.Value.parentSheetIndex) >= 0)
                    AnimalProductsToCollect.Add(new TaskItem<Object>(coop, pair.Key, pair.Value));
        }

        private static void CheckForTruffles(GameLocation farm)
        {
            foreach (var pair in farm.objects)
                if (pair.Value.parentSheetIndex == 430)
                    TrufflesToCollect.Add(new TaskItem<Object>(farm, pair.Key, pair.Value));
        }

        public override string GeneralInfo(out int usedLines)
        {
            usedLines = 0;

            if (!Enabled) return "";

            var count = 0;
            usedLines = 1;

            UpdateList();

            switch (_id)
            {
                case AnimalsTaskId.UnpettedAnimals:
                    if (UnpettedAnimals.Count > 0)
                        return $"Not petted animals: {UnpettedAnimals.Count}^";
                    break;
                case AnimalsTaskId.AnimalProducts:
                    if (_config.AnimalProducts["Truffle"])
                        count = TrufflesToCollect.Count;
                    count += AnimalProductsToCollect.Count(p => _config.ProductToCollect(p.Object.parentSheetIndex));
                    count += AnimalProductsToHarvest.Count(p => _config.ProductFromAnimal(p.Object.currentProduce));
                    if (count > 0)
                        return $"Uncollected animal products: {count}^";
                    break;
                case AnimalsTaskId.MissingHay:
                    count = MissingHay.Sum(t => t.Item2);
                    if (count > 0)
                        return $"Empty hay spots on feeding benches: {count}^";
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknows animal task");
            }

            usedLines = 0;

            return "";
        }

        public override string DetailedInfo(out int usedLines, out bool skipNextPage)
        {
            usedLines = 0;
            skipNextPage = false;

            if (!Enabled) return "";

            var stringBuilder = new StringBuilder();

            switch (_id)
            {
                case AnimalsTaskId.UnpettedAnimals:
                    if (UnpettedAnimals.Count == 0) return "";

                    stringBuilder.Append("Not petted animals:^");
                    usedLines++;

                    foreach (var animal in UnpettedAnimals)
                    {
                        stringBuilder.Append(
                            $"{animal.Object.type} {animal.Object.displayName} at {animal.Location.name} ({animal.Object.getTileX()}, {animal.Object.getTileY()})^");
                        usedLines++;
                    }
                    break;

                case AnimalsTaskId.AnimalProducts:
                    if (AnimalProductsToCollect.Count + TrufflesToCollect.Count + AnimalProductsToHarvest.Count ==
                        0) return "";

                    stringBuilder.Append("Animal products:^");
                    usedLines++;

                    foreach (var animal in AnimalProductsToHarvest)
                    {
                        if (!_config.ProductFromAnimal(animal.Object.currentProduce)) continue;
                        stringBuilder.Append(
                            $"{animal.Object.type} {animal.Object.displayName} has {ObjectsNames[animal.Object.currentProduce]} at {animal.Location.name} ({animal.Object.getTileX()}, {animal.Object.getTileY()})^");
                        usedLines++;
                    }

                    foreach (var product in AnimalProductsToCollect)
                    {
                        if (!_config.ProductToCollect(product.Object.parentSheetIndex)) continue;
                        stringBuilder.Append(
                            $"{product.Object.Name} at {product.Location.name} ({product.Position.X}, {product.Position.Y})^");
                        usedLines++;
                    }

                    if (!_config.AnimalProducts["Truffle"]) break;
                    foreach (var product in TrufflesToCollect)
                    {
                        stringBuilder.Append(
                            $"{product.Object.Name} at {product.Location.name} ({product.Position.X}, {product.Position.Y})^");
                        usedLines++;
                    }
                    break;

                case AnimalsTaskId.MissingHay:
                    if (MissingHay.Count == 0) return "";

                    stringBuilder.Append("Feedbenches not full of hay:^");
                    usedLines++;

                    foreach (var tuple in MissingHay)
                    {
                        var s = "s";
                        if (tuple.Item2 == 1)
                            s = string.Empty;
                        stringBuilder.Append(
                            $"{tuple.Item2} hay{s} missing at {tuple.Item1.indoors.Name} ({tuple.Item1.tileX}, {tuple.Item1.tileY})^");
                        usedLines++;
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Animal task not implemented");
            }

            return stringBuilder.ToString();
        }

        public override void Clear()
        {
            switch (_id)
            {
                case AnimalsTaskId.UnpettedAnimals:
                    Enabled = _config.UnpettedAnimals;
                    break;
                case AnimalsTaskId.AnimalProducts:
                    Enabled = _config.AnimalProducts.ContainsValue(true);
                    break;
                case AnimalsTaskId.MissingHay:
                    Enabled = _config.MissingHay;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Animal task not implemented");
            }
            UnpettedAnimals.Clear();
            AnimalProductsToHarvest.Clear();
            AnimalProductsToCollect.Clear();
            TrufflesToCollect.Clear();
            MissingHay.Clear();
        }
    }

    public enum AnimalsTaskId
    {
        UnpettedAnimals = 0,
        AnimalProducts = 1,
        MissingHay = 2
    }
}