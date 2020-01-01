﻿namespace FeatureFlowFramework.Helper
{
    public struct LazySlim<T> where T : class, new()
    {
        private T obj;

        public T Obj
        {
            get => obj ?? Create();
            set => obj = value;
        }

        public T ObjIfExists => obj;

        public bool IsInstantiated => obj != null;

        private T Create()
        {
            obj = new T();
            return obj;
        }

        public static implicit operator T(LazySlim<T> lazy) => lazy.Obj;
    }
}