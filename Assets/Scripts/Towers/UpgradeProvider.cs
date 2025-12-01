using System;
using UnityEngine;

namespace Towers
{
    public class UpgradeProvider
    {
        public enum DamageType
        {
            Direct,
            AreaOfEffect,
            DoT
        }

        public Action<OnFireData> OnFire;

        // The Events
        public Action<OnHitData> OnHit;
        public Action<OnKillData> OnKill;

        // Data Packets
        public struct OnHitData
        {
            public GameObject Origin;
            public GameObject Target;
            public float Damage;
            public DamageType DamageType;
        }

        public struct OnKillData
        {
            public GameObject Origin;
            public GameObject Target;
            public float Damage;
        }

        public struct OnFireData
        {
            public GameObject Origin;
            public GameObject Target;
        }
    }
}