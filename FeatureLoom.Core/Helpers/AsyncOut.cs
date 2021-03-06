﻿namespace FeatureLoom.Helpers
{
    public readonly struct AsyncOut<RET, OUT>
    {
        private readonly RET returnValue;
        private readonly OUT result;

        public AsyncOut(RET returnValue, OUT result)
        {
            this.returnValue = returnValue;
            this.result = result;
        }

        public RET Out(out OUT result)
        {
            result = this.result;
            return returnValue;
        }

        public RET ReturnValue => returnValue;

        public OUT OutResult => result;

        public static implicit operator AsyncOut<RET, OUT>((RET returnValue, OUT result) tuple) => new AsyncOut<RET, OUT>(tuple.returnValue, tuple.result);
    }

    public readonly struct AsyncOut<RET, OUT1, OUT2>
    {
        private readonly RET returnValue;
        private readonly OUT1 result1;
        private readonly OUT2 result2;

        public AsyncOut(RET returnValue, OUT1 result1, OUT2 result2)
        {
            this.returnValue = returnValue;
            this.result1 = result1;
            this.result2 = result2;
        }

        public RET Out(out OUT1 result1, out OUT2 result2)
        {
            result1 = this.result1;
            result2 = this.result2;
            return returnValue;
        }

        public RET ReturnValue => returnValue;

        public OUT1 OutResult1 => result1;
        public OUT2 OutResult2 => result2;

        public static implicit operator AsyncOut<RET, OUT1, OUT2>((RET returnValue, OUT1 result1, OUT2 result2) tuple) => new AsyncOut<RET, OUT1, OUT2>(tuple.returnValue, tuple.result1, tuple.result2);
    }
}