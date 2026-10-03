using System;

namespace NzbDrone.Core.Parser.Model
{
    // D2/D7 canonical union. SERIES bit values win for every shared semantic; the
    // MOVIES-only flags (PTP_*, AHD_UserRelease) are re-numbered onto free bits.
    // MOVIES' G_* names are kept as aliases so the ported movie sources compile and
    // compare against the same canonical values. D9 owns the persisted data remap
    // (Nuked 2048 -> 128, G_* -> canonical).
    [Flags]
    public enum IndexerFlags
    {
        /// <summary>
        /// Torrent download amount does not count
        /// </summary>
        Freeleech = 1,

        /// <summary>
        /// Torrent download amount only counts 50%
        /// </summary>
        Halfleech = 2,

        /// <summary>
        /// Torrent upload amount is doubled
        /// </summary>
        DoubleUpload = 4,

        /// <summary>
        /// Uploader is an internal release group
        /// </summary>
        Internal = 8,

        /// <summary>
        /// The release comes from a scene group
        /// </summary>
        Scene = 16,

        /// <summary>
        /// Torrent download amount only counts 75%
        /// </summary>
        Freeleech75 = 32,

        /// <summary>
        /// Torrent download amount only counts 25%
        /// </summary>
        Freeleech25 = 64,

        /// <summary>
        /// The release is nuked
        /// </summary>
        Nuked = 128,

        /// <summary>
        /// The release contains subtitles
        /// </summary>
        Subtitles = 256,

        /// <summary>
        /// The release is exclusive to the indexer/tracker and must not be uploaded elsewhere
        /// </summary>
        Exclusive = 4096,

        /// <summary>
        /// Torrent download and upload do not count toward ratio
        /// </summary>
        NeutralLeech = 8192,

        // ----- MOVIES aliases (canonical SERIES values) -----

        /// <summary>
        /// Torrent download amount does not count (MOVIES alias)
        /// </summary>
        G_Freeleech = 1,

        /// <summary>
        /// Torrent download amount only counts 50% (MOVIES alias)
        /// </summary>
        G_Halfleech = 2,

        /// <summary>
        /// Torrent upload amount is doubled (MOVIES alias)
        /// </summary>
        G_DoubleUpload = 4,

        /// <summary>
        /// Uploader is an internal release group (MOVIES alias)
        /// </summary>
        G_Internal = 8,

        /// <summary>
        /// The release comes from a scene group (MOVIES alias)
        /// </summary>
        G_Scene = 16,

        /// <summary>
        /// Torrent download amount only counts 75% (MOVIES alias)
        /// </summary>
        G_Freeleech75 = 32,

        /// <summary>
        /// Torrent download amount only counts 25% (MOVIES alias)
        /// </summary>
        G_Freeleech25 = 64,

        // ----- MOVIES-only flags (free canonical bits) -----

        /// <summary>
        /// Torrent is a very high quality encode, as applied manually by the PTP staff
        /// </summary>
        PTP_Golden = 512,

        /// <summary>
        /// Torrent from PTP that has been checked for release description requirements
        /// </summary>
        PTP_Approved = 1024,

        // AHD internal (obsolete, remapped to Internal by D9)
        [Obsolete]
        AHD_Internal = 8,

        // AHD user release (MOVIES-only)
        [Obsolete]
        AHD_UserRelease = 2048
    }
}
