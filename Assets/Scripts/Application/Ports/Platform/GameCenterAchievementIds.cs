namespace Witch.Application.Ports
{
    /// <summary>
    /// Stable Apple Game Center achievement identifiers.
    ///
    /// Full IDs must use the permanent bundle namespace and must never be
    /// renamed after publishing: an achievement available to players cannot
    /// be deleted and its point value cannot be changed. The namespace must
    /// match the permanent app bundle identifier.
    ///
    /// This is the only file that may contain achievement ID strings; presenters
    /// and use cases reference these constants instead.
    /// </summary>
    public static class GameCenterAchievementIds
    {
        /// <summary>
        /// Must match ProjectSettings applicationIdentifier for macOS.
        /// </summary>
        public const string BundleNamespace = "com.dzikryaji.witch";

        // Phase 1: first release (approved scope)
        public const string FirstShiftSuffix = "achievement.first_shift";
        public const string SecretKeptSuffix = "achievement.secret_kept";

        // Phase 2: proposed achievements, require scope approval before release
        public const string FirstRemedySuffix = "achievement.first_remedy";
        public const string MemoryMenderSuffix = "achievement.memory_mender";
        public const string FiveArtsSuffix = "achievement.five_arts";
        public const string CleanLedgerSuffix = "achievement.clean_ledger";

        /// <summary>
        /// First Shift: paid the Day 1 expense and the next-day save succeeded.
        /// </summary>
        public static string FirstShift => BundleNamespace + "." + FirstShiftSuffix;

        /// <summary>
        /// Secret Kept: paid the final Day 14 expense and the completed run
        /// save succeeded.
        /// </summary>
        public static string SecretKept => BundleNamespace + "." + SecretKeptSuffix;

        /// <summary>
        /// First Remedy: first correct potion served.
        /// </summary>
        public static string FirstRemedy => BundleNamespace + "." + FirstRemedySuffix;

        /// <summary>
        /// Memory Mender: first Match Card spell completed.
        /// </summary>
        public static string MemoryMender => BundleNamespace + "." + MemoryMenderSuffix;

        /// <summary>
        /// Five Arts: correctly served one order from every treatment category.
        /// </summary>
        public static string FiveArts => BundleNamespace + "." + FiveArtsSuffix;

        /// <summary>
        /// Clean Ledger: Day 2 or later, at least one customer served, no wrong service that day, and no rewind minigame played that day.
        /// </summary>
        public static string CleanLedger => BundleNamespace + "." + CleanLedgerSuffix;
    }
}
