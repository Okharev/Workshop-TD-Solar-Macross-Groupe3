using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Towers
{
    public class StatusHandler : MonoBehaviour
    {
        private readonly Dictionary<Type, IStatusEffect> activeEffects = new();
        private readonly Dictionary<Type, Coroutine> activeRoutines = new();

        public event Action<IStatusEffect> OnStatusAdded;
        public event Action<IStatusEffect> OnStatusUpdated;
        public event Action<Type> OnStatusRemoved;

        public void ApplyStatus(IStatusEffect effect)
        {
            var type = effect.GetType();

            if (activeEffects.TryGetValue(type, out var existingEffect))
            {
                existingEffect.Reapply(effect);

                OnStatusUpdated?.Invoke(existingEffect);
            }
            else
            {
                activeEffects.Add(type, effect);
                effect.OnApply(this);

                var routine = StartCoroutine(RunEffectRoutine(effect));
                activeRoutines.Add(type, routine);

                OnStatusAdded?.Invoke(effect);
            }
        }

        public void RemoveStatus(Type type)
        {
            if (activeEffects.TryGetValue(type, out var effect))
            {
                effect.OnEnd();

                if (activeRoutines.TryGetValue(type, out var routine))
                {
                    StopCoroutine(routine);
                    activeRoutines.Remove(type);
                }

                activeEffects.Remove(type);

                OnStatusRemoved?.Invoke(type);
            }
        }

        private IEnumerator RunEffectRoutine(IStatusEffect effect)
        {
            yield return effect.Process(this);

            RemoveStatus(effect.GetType());
        }
    }
}