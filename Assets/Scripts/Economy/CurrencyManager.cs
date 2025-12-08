using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Economy
{
    [DefaultExecutionOrder(-200)]
    public sealed class CurrencyManager : MonoBehaviour
    {
        [SerializeField]
        private int startingMoney = 500;
        [SerializeField]
        private ReactiveInt currentMoney;

        public IReadOnlyReactiveProperty<int> CurrentMoney => currentMoney;

        public event Action FailedToSpend;
        
        public static CurrencyManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            currentMoney = new ReactiveInt(startingMoney);

            CurrentMoney.Subscribe((i) => Debug.Log(i)).AddTo(this);
        }
        
        public int GetBalance()
        {
            return currentMoney.Value;
        }

        public bool CanAfford(int cost)
        {
            return currentMoney.Value >= cost;
        }

        public bool TrySpend(int amount)
        {
            if (currentMoney.Value - amount < 0)
            {
                FailedToSpend?.Invoke();

                return false;
            }
            currentMoney.Value -= amount;

            return true;
        }

        public void Gain(int amount)
        {
            currentMoney.Value = currentMoney.Value + amount;
        }
    }
}