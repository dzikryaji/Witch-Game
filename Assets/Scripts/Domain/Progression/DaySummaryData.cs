using System;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class DaySummaryData
    {
        public DaySummaryData(
            int correctOrders,
            int wrongOrders,
            int coinsEarned,
            int taxPaid,
            int coinsRemaining,
            int dayNumber)
        {
            if (correctOrders < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(correctOrders));
            }

            if (wrongOrders < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wrongOrders));
            }

            if (coinsEarned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coinsEarned));
            }

            if (taxPaid < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(taxPaid));
            }

            if (coinsRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coinsRemaining));
            }

            if (dayNumber < RunState.FirstDay)
            {
                throw new ArgumentOutOfRangeException(nameof(dayNumber));
            }

            CorrectOrders = correctOrders;
            WrongOrders = wrongOrders;
            CoinsEarned = coinsEarned;
            TaxPaid = taxPaid;
            CoinsRemaining = coinsRemaining;
            DayNumber = dayNumber;
        }

        public int CorrectOrders { get; }
        public int WrongOrders { get; }
        public int CoinsEarned { get; }
        public int TaxPaid { get; }
        public int CoinsRemaining { get; }
        public int DayNumber { get; }
    }
}
