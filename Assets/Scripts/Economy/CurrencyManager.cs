using System;
using UnityEngine;

namespace Economy
{
    [DefaultExecutionOrder(-100)]
    public class CurrencyManager : MonoBehaviour
    {
        [SerializeField] private int startingMoney = 500;
        private int _currentMoney;
        public static CurrencyManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _currentMoney = startingMoney;
        }

        public event Action<int> OnMoneyChanged;

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
            _currentMoney -= amount;
            OnMoneyChanged?.Invoke(_currentMoney);
        }

        public void Gain(int amount)
        {
            _currentMoney += amount;
            OnMoneyChanged?.Invoke(_currentMoney);
        }
    }
}