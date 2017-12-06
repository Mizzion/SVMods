﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DailyTasksReport.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DailyTasksReport.Tasks
{
    public class CropsTask : Task
    {
        private readonly ModConfig _config;
        private readonly CropsTaskId _id;
        private readonly int _index;
        private readonly string _locationName;
        private bool _anyCrop;
        
        // 0 = Farm, 1 = Greenhouse
        private static readonly List<Tuple<Vector2, HoeDirt>>[] Crops = {new List<Tuple<Vector2, HoeDirt>>(), new List<Tuple<Vector2, HoeDirt>>()};
        private static bool _doneScan;
        private static CropsTaskId _who = CropsTaskId.None;

        internal CropsTask(ModConfig config, CropsTaskId id)
        {
            _config = config;
            _id = id;

            if (id == CropsTaskId.UnwateredCropFarm || id == CropsTaskId.UnharvestedCropFarm ||
                id == CropsTaskId.DeadCropFarm)
            {
                _index = 0;
                _locationName = "Farm";
            }
            else
            {
                _index = 1;
                _locationName = "Greenhouse";
            }

            if (ObjectsNames.Count == 0)
                PopulateObjectsNames();

            SettingsMenu.ReportConfigChanged += SettingsMenu_ReportConfigChanged;
        }

        private void SettingsMenu_ReportConfigChanged(object sender, SettingsChangedEventArgs e)
        {
            switch (_id)
            {
                case CropsTaskId.UnwateredCropFarm:
                case CropsTaskId.UnwateredCropGreenhouse:
                    Enabled = _config.UnwateredCrops;
                    break;
                case CropsTaskId.UnharvestedCropFarm:
                case CropsTaskId.UnharvestedCropGreenhouse:
                    Enabled = _config.UnharvestedCrops;
                    break;
                case CropsTaskId.DeadCropFarm:
                case CropsTaskId.DeadCropGreenhouse:
                    Enabled = _config.DeadCrops;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Crop task or location not implemented");
            }
        }

        public override void FirstScan()
        {
            if (_who == CropsTaskId.None)
                _who = _id;
        }

        public override string GeneralInfo(out int usedLines)
        {
            usedLines = 0;

            if (!Enabled) return "";

            _anyCrop = false;
            usedLines = 1;
            int count;

            if (!_doneScan && Crops[_index].Count == 0)
            {
                _doneScan = true;

                var location = Game1.locations.Find(l => l.name == _locationName);
                foreach (var keyValuePair in location.terrainFeatures)
                    if (keyValuePair.Value is HoeDirt dirt && dirt.crop != null)
                        Crops[_index].Add(new Tuple<Vector2, HoeDirt>(keyValuePair.Key, dirt));
            }

            switch (_id)
            {
                case CropsTaskId.UnwateredCropFarm:
                case CropsTaskId.UnwateredCropGreenhouse:
                    count = Crops[_index].Count(pair =>
                        pair.Item2.state == HoeDirt.dry && pair.Item2.needsWatering() && !pair.Item2.crop.dead);
                    if (count > 0)
                    {
                        _anyCrop = true;
                        return $"{_locationName} crops not watered: {count}^";
                    }
                    break;

                case CropsTaskId.UnharvestedCropFarm:
                case CropsTaskId.UnharvestedCropGreenhouse:
                    count = Crops[_index].Count(pair => pair.Item2.readyForHarvest());
                    if (count > 0)
                    {
                        _anyCrop = true;
                        return $"{_locationName} crops ready to harvest: {count}^";
                    }
                    break;

                case CropsTaskId.DeadCropFarm:
                case CropsTaskId.DeadCropGreenhouse:
                    count = Crops[_index].Count(pair => pair.Item2.crop.dead);
                    if (count > 0)
                    {
                        _anyCrop = true;
                        return $"Dead crops in the {_locationName}: {count}^";
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Crop task or location not implemented");
            }

            usedLines = 0;
            return "";
        }

        public override string DetailedInfo(out int usedLines, out bool skipNextPage)
        {
            usedLines = 0;
            skipNextPage = false;

            if (!Enabled || !_anyCrop) return "";

            var stringBuilder = new StringBuilder();

            switch (_id)
            {
                case CropsTaskId.UnwateredCropFarm:
                    stringBuilder.Append("Unwatered crops:^");
                    usedLines++;
                    skipNextPage = true;
                    break;
                case CropsTaskId.UnharvestedCropFarm:
                    stringBuilder.Append("Ready to harvest crops:^");
                    usedLines++;
                    skipNextPage = true;
                    break;
                case CropsTaskId.DeadCropFarm:
                    stringBuilder.Append("Dead crops:^");
                    usedLines++;
                    skipNextPage = true;
                    break;
            }

            switch (_id)
            {
                case CropsTaskId.UnwateredCropFarm:
                case CropsTaskId.UnwateredCropGreenhouse:
                    EchoForCrops(ref stringBuilder, ref usedLines, pair =>
                        pair.Item2.state == HoeDirt.dry && pair.Item2.needsWatering() && !pair.Item2.crop.dead);
                    break;
                case CropsTaskId.UnharvestedCropFarm:
                case CropsTaskId.UnharvestedCropGreenhouse:
                    EchoForCrops(ref stringBuilder, ref usedLines, pair => pair.Item2.readyForHarvest());
                    break;
                case CropsTaskId.DeadCropFarm:
                case CropsTaskId.DeadCropGreenhouse:
                    EchoForCrops(ref stringBuilder, ref usedLines, pair => pair.Item2.crop.dead);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Crop task or location not implemented");
            }

            return stringBuilder.ToString();
        }

        private void EchoForCrops(ref StringBuilder stringBuilder, ref int count,
            Func<Tuple<Vector2, HoeDirt>, bool> predicate)
        {
            foreach (var item in Crops[_index].Where(predicate))
            {
                stringBuilder.Append(
                    $"{ObjectsNames[item.Item2.crop.indexOfHarvest]} at {_locationName} ({item.Item1.X}, {item.Item1.Y})^");
                count++;
            }
        }

        public override void FinishedReport()
        {
            Crops[_index].Clear();
            _doneScan = false;
        }

        public override void Draw(SpriteBatch b)
        {
            if ((Game1.currentLocation is Farm || Game1.currentLocation.name == "Greenhouse") && _who == _id)
            {
                var x = Game1.viewport.X / Game1.tileSize;
                var xLimit = (Game1.viewport.X + Game1.viewport.Width) / Game1.tileSize;
                var yStart = Game1.viewport.Y / Game1.tileSize;
                var yLimit = (Game1.viewport.Y + Game1.viewport.Height) / Game1.tileSize;
                for (; x < xLimit; ++x)
                for (var y = yStart; y < yLimit; ++y)
                    if (Game1.currentLocation.terrainFeatures.TryGetValue(new Vector2(x, y), out var _))
                    {
                        var v = new Vector2(x * Game1.tileSize - Game1.viewport.X,
                                            y * Game1.tileSize - Game1.viewport.Y);
                         b.Draw(Game1.mouseCursors, v, new Rectangle(141, 465, 20, 24), Color.White * 0.65f);
                    }
            }
        }

        public override void Clear()
        {
            Crops[_index].Clear();

            switch (_id)
            {
                case CropsTaskId.UnwateredCropFarm:
                case CropsTaskId.UnwateredCropGreenhouse:
                    Enabled = _config.UnwateredCrops;
                    break;
                case CropsTaskId.UnharvestedCropFarm:
                case CropsTaskId.UnharvestedCropGreenhouse:
                    Enabled = _config.UnharvestedCrops;
                    break;
                case CropsTaskId.DeadCropFarm:
                case CropsTaskId.DeadCropGreenhouse:
                    Enabled = _config.DeadCrops;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Crop task or location not implemented");
            }
        }
    }

    public enum CropsTaskId
    {
        None = -1,
        UnwateredCropFarm = 0,
        UnwateredCropGreenhouse = 1,
        UnharvestedCropFarm = 2,
        UnharvestedCropGreenhouse = 3,
        DeadCropFarm = 4,
        DeadCropGreenhouse = 5
    }
}