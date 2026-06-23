using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace AdditionalArmorFeaturesLibrary.Utils
{
    public class ArmorFeaturesProp
    {

        //Current Stats
        //Music vars.
        public string? lightSoundPath { get; set; }
        public string? powerSoundPath { get; set; }
        public string? exstateSoundPath { get; set; }
        public string? jumppackSoundPath { get; set; }
        public string? jetpackSoundPath { get; set; }

        //Power vars.
        public bool FeaturesUsePower { get; set; } = false;

        public float? fuelCapacity;
        public string PowerType { get; set; } = "fuel";
        public bool? UseFuel { get; set; }
        public bool? OnCraftedFueled { get; set; }
        public Dictionary<string, float> FuelList { get; set; } = new();

        //Light var.
        public byte[] lightHSV { get; set; } = new byte[0];

        //Jumppack Var.
        public double jumpForwardVel = 0;
        public double jumpUpwardVel = 0;
        public double jumpDelay = 0;
        public double jumpConsumption = 0;
        //Jetpack Var.
        public double jetMaxUpwardVel = 0.25;
        public double jetUpwardVel = 0.03;
        public double jetConsumption = 0;

        public static ArmorFeaturesProp? ReadFrom(ItemStack itemStack)
        {
            if (itemStack == null)
            {
                return null;
            }

            return ReadFrom(itemStack.Collectible);
        }

        public static ArmorFeaturesProp? ReadFrom(CollectibleObject colObj)
        {
            if (colObj?.Attributes == null) return null;

            var byType = colObj.Attributes["armorFeaturesPropByType"]?.AsObject<Dictionary<string, ArmorFeaturesProp>>();

            if (byType == null)
            {
                return colObj.Attributes["armorFeaturesProp"]?.AsObject<ArmorFeaturesProp>();
            }

            string path = colObj.Code.Path;

            var matches = byType
                .Where(x => WildcardUtil.Match(x.Key, path))
                .OrderByDescending(x => x.Key.Count(c => c == '*'))
                .ToList();

            if (matches.Count == 0)
            {
                return colObj.Attributes["armorFeaturesProp"]
                    ?.AsObject<ArmorFeaturesProp>();
            }

            ArmorFeaturesProp result = new();

            foreach (var match in matches)
            {
                result.Merge(match.Value);
            }

            return result;
        }

        public void Merge(ArmorFeaturesProp other)
        {
            if (other == null) return;

            if (!string.IsNullOrEmpty(other.lightSoundPath))
                lightSoundPath = other.lightSoundPath;

            if (other.fuelCapacity.HasValue)
                fuelCapacity = other.fuelCapacity;

            if (!string.IsNullOrEmpty(other.PowerType))
                PowerType = other.PowerType;

            if (other.UseFuel.HasValue)
                UseFuel = other.UseFuel;

            if (other.OnCraftedFueled.HasValue)
                OnCraftedFueled = other.OnCraftedFueled;

            if (other.FuelList?.Count > 0)
            {
                foreach (var fuel in other.FuelList)
                {
                    FuelList[fuel.Key] = fuel.Value;
                }
            }

            // future:
            // if (other.lightHsv != null)
            //     lightHsv = other.lightHsv;
        }

        public class ParticleProp
        {
            public Vec3d? BasePos { get; set; }

            public AdvancedParticleProperties[] Particle { get; set; } = Array.Empty<AdvancedParticleProperties>();

        }
    }
}
