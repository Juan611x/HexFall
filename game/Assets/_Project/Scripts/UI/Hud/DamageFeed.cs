using System;
using UnityEngine;

namespace HexFall.UI
{
    // Where damage sources announce the damage they took, so presentation (damage numbers) can react
    // without the gameplay code knowing about it.
    public static class DamageFeed
    {
        public static event Action<Vector3, int> Dealt;

        public static void Report(Vector3 at, int amount)
        {
            if (amount <= 0) return;
            Dealt?.Invoke(at, amount);
        }
    }
}
