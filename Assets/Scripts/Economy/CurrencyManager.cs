using System;
using UnityEngine;

namespace Economy
{
    [DefaultExecutionOrder(-100)]
    public class CurrencyManager : MonoBehaviour
    {
        [SerializeField] private int startingMoney = 500;
        private ReactiveInt _currentMoney;

        public IReadOnlyReactiveProperty<int> CurrentMoney => _currentMoney;
        
        public static CurrencyManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _currentMoney = new ReactiveInt(startingMoney);
        }
        
        public int GetBalance()
        {
            return _currentMoney;
        }

        public bool CanAfford(int cost)
        {
            return _currentMoney >= cost;
        }

        public void Spend(int amount)
        {
            if (amount <= 0) return;
            _currentMoney.Value -= amount;
        }

        public void Gain(int amount)
        {
            _currentMoney.Value += amount;
        }
    }
}