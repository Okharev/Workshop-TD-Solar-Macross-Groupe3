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

        public Action<OnFireData> onFire;

        // The Events
        public Action<OnHitData> onHit;
        public Action<OnKillData> onKill;

        // Data Packets
        public struct OnHitData
        {
            public GameObject origin;
            public GameObject target;
            public float damage;
            public DamageType damageType;
        }

        public struct OnKillData
        {
            public GameObject origin;
            public GameObject target;
            public float damage;
        }

        public struct OnFireData
        {
            public GameObject origin;
            public GameObject target;
        }
    }
}