using System;
using System.Runtime.InteropServices;

namespace UW.ClassroomPresenter.Misc {
    class AccurateTiming {
        [DllImport( "kernel32.dll" )]
        public static extern int QueryPerformanceFrequency( ref Int64 lpFrequency );

        [DllImport( "kernel32.dll" )]
        public static extern int QueryPerformanceCounter( ref Int64 lpPerformanceCount );

        /// <summary>
        /// Frequency at which this processor counts ticks in ticks-per-second
        /// </summary>
        private static System.Int64 m_Frequency = 0;
        /// <summary>
        /// True after the frequency has been calculated
        /// </summary>
        private static bool m_Initialized = false;

        /// <summary>
        /// Returns the current number of milli-seconds since this processor has been started
        /// </summary>
        public static Int64 Now {
            get { return GetTickCount(); }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public AccurateTiming() {
            AccurateTiming.Initialize();
        }

        /// <summary>
        /// Calculate the counter frequency for this processor
        /// </summary>
        public static void Initialize() {
            if( !AccurateTiming.m_Initialized ) {
                if( QueryPerformanceFrequency( ref AccurateTiming.m_Frequency ) != 0 )
                    AccurateTiming.m_Initialized = true;
                else
                    throw new NotSupportedException( "Hardware Timers Not Supported" );
            }
        }

        /// <summary>
        /// Get the current number of milli-seconds since this process has been started
        /// </summary>
        /// <returns>Milli-seconds</returns>
        public static Int64 GetTickCount() {
            AccurateTiming.Initialize();
            System.Int64 count = 0;
            QueryPerformanceCounter(ref count);
            System.Int64 time_ns = (count) * 1000 / AccurateTiming.m_Frequency;
            return time_ns;
        }
    }
}
