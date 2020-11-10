using System;
using UnityEngine;

namespace Runtime.AtmoGpu
{
    public struct RenderTextureRotating : IDisposable
    {
        public RenderTexture Write, Read;

        public RenderTextureRotating(int width, int height, RenderTextureFormat format, FilterMode filter)
        {
            Write = new RenderTexture(width, height, 0, format)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp
            };
            Write.Create();

            Read = new RenderTexture(width, height, 0, format)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp
            };
            Read.Create();
        }

        public void Swap()
        {
            var temp = Write;
            Write = Read;
            Read = temp;
        }

        public void Dispose()
        {
            Write.Release();
            Read.Release();
        }
    }
}