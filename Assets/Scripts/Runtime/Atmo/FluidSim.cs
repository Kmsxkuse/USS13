using System;
using Components;
using UnityEngine;

// ReSharper disable Unity.PreferAddressByIdToGraphicsParams

namespace Runtime.Atmo
{
    public class FluidSim : MonoBehaviour
    {
        public Color FluidColor = Color.red;

        public Color ObstacleColor = Color.white;
      
        public Material GUIMat, AdvectMat, BuoyancyMat, DivergenceMat, JacobiMat, ImpluseMat, GradientMat, ObstaclesMat;

        private RenderTexture _guiTex, _divergenceTex, _obstaclesTex;
        private RenderTextureRotating _velocityTex, _densityTex, _pressureTex, _temperatureTex;

        private const float ImpulseTemperature = 10.0f;
        private const float ImpulseDensity = 1.0f;
        private const float TemperatureDissipation = 0.99f;
        private const float VelocityDissipation = 0.99f;
        private const float DensityDissipation = 0.9999f;
        private const float AmbientTemperature = 0.0f;
        private const float SmokeBuoyancy = 1.0f;
        private const float SmokeWeight = 0.05f;

        private const float CellSize = 1.0f;
        private const float GradientScale = 1.0f;

        private Vector2 _inverseSize;
        private const int NumJacobiIterations = 50;

        private Vector2 _implusePos = new Vector2(0.5f, 0.0f);
        private const float ImpulseRadius = 0.1f;
        private const float MouseImpulseRadius = 0.05f;

        private Vector2 _obstaclePos = new Vector2(0.5f, 0.5f);
        private const float ObstacleRadius = 0.1f;

        private Rect _rect;
        private int _width, _height;

        private void Start()
        {
            _width = 512;
            _height = 512;

            Vector2 size = new Vector2(_width, _height);
            Vector2 pos = new Vector2(Screen.width / 2, Screen.height / 2) - size * 0.5f;
            _rect = new Rect(pos, size);

            _inverseSize = new Vector2(1.0f / _width, 1.0f / _height);

            _velocityTex = new RenderTextureRotating(_width, _height, RenderTextureFormat.RGFloat, FilterMode.Bilinear);
            _densityTex = new RenderTextureRotating(_width, _height, RenderTextureFormat.RFloat, FilterMode.Bilinear);
            _temperatureTex = new RenderTextureRotating(_width, _height, RenderTextureFormat.RFloat, FilterMode.Bilinear);
            _pressureTex = new RenderTextureRotating(_width, _height, RenderTextureFormat.RFloat, FilterMode.Point);

            _guiTex = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear, 
                wrapMode = TextureWrapMode.Clamp
            };
            _guiTex.Create();

            _divergenceTex = new RenderTexture(_width, _height, 0, RenderTextureFormat.RFloat)
            {
                filterMode = FilterMode.Point, 
                wrapMode = TextureWrapMode.Clamp
            };
            _divergenceTex.Create();

            _obstaclesTex = new RenderTexture(_width, _height, 0, RenderTextureFormat.RFloat)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _obstaclesTex.Create();
        }

        private void OnGUI()
        {
            GUI.DrawTexture(_rect, _guiTex);
        }

        private void Advect(RenderTexture velocity, RenderTexture source, RenderTexture dest, float dissipation, float timeStep)
        {
            AdvectMat.SetVector("_InverseSize", _inverseSize);
            AdvectMat.SetFloat("_TimeStep", timeStep);
            AdvectMat.SetFloat("_Dissipation", dissipation);
            AdvectMat.SetTexture("_Velocity", velocity);
            AdvectMat.SetTexture("_Source", source);
            AdvectMat.SetTexture("_Obstacles", _obstaclesTex);

            Graphics.Blit(null, dest, AdvectMat);
        }

        private void ApplyBuoyancy(RenderTexture velocity, RenderTexture temperature, RenderTexture density, RenderTexture dest, float timeStep)
        {
            BuoyancyMat.SetTexture("_Velocity", velocity);
            BuoyancyMat.SetTexture("_Temperature", temperature);
            BuoyancyMat.SetTexture("_Density", density);
            BuoyancyMat.SetFloat("_AmbientTemperature", AmbientTemperature);
            BuoyancyMat.SetFloat("_TimeStep", timeStep);
            BuoyancyMat.SetFloat("_Sigma", SmokeBuoyancy);
            BuoyancyMat.SetFloat("_Kappa", SmokeWeight);

            Graphics.Blit(null, dest, BuoyancyMat);
        }

        private void ApplyImpulse(RenderTexture source, RenderTexture dest, Vector2 pos, float radius, float val)
        {
            ImpluseMat.SetVector("_Point", pos);
            ImpluseMat.SetFloat("_Radius", radius);
            ImpluseMat.SetFloat("_Fill", val);
            ImpluseMat.SetTexture("_Source", source);

            Graphics.Blit(null, dest, ImpluseMat);
        }

        private void ComputeDivergence(RenderTexture velocity, RenderTexture dest)
        {
            DivergenceMat.SetFloat("_HalfInverseCellSize", 0.5f / CellSize);
            DivergenceMat.SetTexture("_Velocity", velocity);
            DivergenceMat.SetVector("_InverseSize", _inverseSize);
            DivergenceMat.SetTexture("_Obstacles", _obstaclesTex);

            Graphics.Blit(null, dest, DivergenceMat);
        }

        private void Jacobi(RenderTexture pressure, RenderTexture divergence, RenderTexture dest)
        {

            JacobiMat.SetTexture("_Pressure", pressure);
            JacobiMat.SetTexture("_Divergence", divergence);
            JacobiMat.SetVector("_InverseSize", _inverseSize);
            JacobiMat.SetFloat("_Alpha", -CellSize * CellSize);
            JacobiMat.SetFloat("_InverseBeta", 0.25f);
            JacobiMat.SetTexture("_Obstacles", _obstaclesTex);

            Graphics.Blit(null, dest, JacobiMat);
        }

        private void SubtractGradient(RenderTexture velocity, RenderTexture pressure, RenderTexture dest)
        {
            GradientMat.SetTexture("_Velocity", velocity);
            GradientMat.SetTexture("_Pressure", pressure);
            GradientMat.SetFloat("_GradientScale", GradientScale);
            GradientMat.SetVector("_InverseSize", _inverseSize);
            GradientMat.SetTexture("_Obstacles", _obstaclesTex);

            Graphics.Blit(null, dest, GradientMat);
        }

        private void AddObstacles()
        {
            ObstaclesMat.SetVector("_InverseSize", _inverseSize);
            ObstaclesMat.SetVector("_Point", _obstaclePos);
            ObstaclesMat.SetFloat("_Radius", ObstacleRadius);

            Graphics.Blit(null, _obstaclesTex, ObstaclesMat);
        }

        private void ClearSurface(RenderTexture surface)
        {
            Graphics.SetRenderTarget(surface);
            GL.Clear(false, true, new Color(0, 0, 0, 0));
            Graphics.SetRenderTarget(null);
        }

        private void FixedUpdate()
        {
            //Obstacles only need to be added once unless changed.
            AddObstacles();

            //Set the density field and obstacle color.
            GUIMat.SetColor("_FluidColor", FluidColor);
            GUIMat.SetColor("_ObstacleColor", ObstacleColor);
            
            var dt = Time.fixedDeltaTime * 10;

            //Advect velocity against its self
            Advect(_velocityTex.Read, _velocityTex.Read, _velocityTex.Write, VelocityDissipation, dt);
            //Advect temperature against velocity
            Advect(_velocityTex.Read, _temperatureTex.Read, _temperatureTex.Write, TemperatureDissipation, dt);
            //Advect density against velocity
            Advect(_velocityTex.Read, _densityTex.Read, _densityTex.Write, DensityDissipation, dt);

            _velocityTex.Swap();
            _temperatureTex.Swap();
            _densityTex.Swap();

            //Determine how the flow of the fluid changes the velocity
            ApplyBuoyancy(_velocityTex.Read, _temperatureTex.Read, _densityTex.Read, _velocityTex.Write, dt);

            _velocityTex.Swap();

            //Refresh the impulse of density and temperature
            ApplyImpulse(_temperatureTex.Read, _temperatureTex.Write, _implusePos, ImpulseRadius, ImpulseTemperature);
            ApplyImpulse(_densityTex.Read, _densityTex.Write, _implusePos, ImpulseRadius, ImpulseDensity);

            _temperatureTex.Swap();
            _densityTex.Swap();

            //If left click down add impulse, if right click down remove impulse from mouse pos.
            if(Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                Vector2 pos = Input.mousePosition;

                pos.x -= _rect.xMin;
                pos.y -= _rect.yMin;

                pos.x /= _rect.width;
                pos.y /= _rect.height;

                float sign = Input.GetMouseButton(0) ? 1.0f : -1.0f;

                ApplyImpulse(_temperatureTex.Read, _temperatureTex.Write, pos, MouseImpulseRadius, ImpulseTemperature);
                ApplyImpulse(_densityTex.Read, _densityTex.Write, pos, MouseImpulseRadius, ImpulseDensity * sign);

                _temperatureTex.Swap();
                _densityTex.Swap();
            }

            //Calculates how divergent the velocity is
            ComputeDivergence(_velocityTex.Read, _divergenceTex);

            ClearSurface(_pressureTex.Read);

            for (var i = 0; i < NumJacobiIterations; ++i)
            {
                Jacobi(_pressureTex.Read, _divergenceTex, _pressureTex.Write);
                _pressureTex.Swap();
            }

            //Use the pressure tex that was last rendered into. This computes divergence free velocity
            SubtractGradient(_velocityTex.Read, _pressureTex.Read, _velocityTex.Write);

            _velocityTex.Swap();

            //Render the tex you want to see into gui tex. Will only use the red channel
            GUIMat.SetTexture("_Obstacles", _obstaclesTex);
            Graphics.Blit(_densityTex.Read, _guiTex, GUIMat);
        }

        private void OnDestroy()
        {
            _guiTex.Release();
            _divergenceTex.Release();
            _obstaclesTex.Release();
            _velocityTex.Dispose();
            _densityTex.Dispose();
            _pressureTex.Dispose();
            _temperatureTex.Dispose();
        }
    }

}
