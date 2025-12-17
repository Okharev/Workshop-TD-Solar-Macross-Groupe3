using System;
using UnityEngine;

namespace Towers
{
    public sealed class UpgradeProvider
    {
        public enum DamageType
        {
            Direct,
            AreaOfEffect,
            DoT
        }
        // The Events
        public Action<OnFireData> OnFire;
        public Action<OnHitData> OnHit;
        public Action<OnKillData> OnKill;

        // Data Packets
        public struct OnHitData
        {
            public GameObject Origin;
            public GameObject Target;
            public float Damage;
            public DamageType DamageType;
            public Vector3 HitPosition;
            public Vector3 HitNormal;
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