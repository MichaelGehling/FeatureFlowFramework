﻿using System;

namespace FeatureLoom.Time
{
    public struct TimeKeeper
    {
        private TimeSpan startTime;
        private TimeSpan lastElapsed;

        public TimeKeeper(TimeSpan startTime)
        {
            Restart(startTime);
        }

        public TimeSpan Elapsed
        {
            get
            {
                lastElapsed = AppTime.Elapsed - startTime;
                return lastElapsed;
            }
        }

        public TimeSpan LastElapsed => lastElapsed;

        public void Restart() => Restart(AppTime.Elapsed);

        public void Restart(TimeSpan startTime)
        {
            this.startTime = startTime;
            this.lastElapsed = startTime;
        }

        public TimeSpan StartTime => startTime;        
    }
}