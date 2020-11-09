using System;
using Unity.Mathematics;
using UnityEngine;

namespace Components
{
    public struct RenderTextureRotating : IDisposable
    {
        public RenderTexture Write, Read;

        public RenderTextureRotating(int width, int height, RenderTextureFormat format, FilterMode filter,
            RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default)
        {
            Write = new RenderTexture(width, height, 0, format, readWrite)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp
            };
            Write.Create();
            
            Read = new RenderTexture(width, height, 0, format, readWrite)
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