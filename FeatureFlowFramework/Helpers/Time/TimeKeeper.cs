﻿using FeatureLoom.Services;
using System;

namespace FeatureLoom.Helpers.Time
{
    public struct TimeKeeper
    {
        private TimeSpan startTime;
        private TimeSpan lastElapsed;

        public TimeKeeper(TimeSpan startTime)
        {
            this.startTime = startTime;
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

        public void Restart() => this.startTime = AppTime.Elapsed;
        public void Restart(TimeSpan startTime) => this.startTime = startTime;

        public TimeSpan StartTime => startTime;
    }
}