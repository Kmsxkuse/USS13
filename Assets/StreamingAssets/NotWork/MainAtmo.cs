using System.Collections.Generic;
using Components;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Runtime.NotWork
{
    public class MainAtmo : MonoBehaviour
    {
        // Should be attached to Grid.
        public GameObject TileMapObject;
        public ComputeShader AdventShader, BuoyancyShader, ImpulseShader, DivergenceShader,
            JacobiShader, SubtractGradientShader, ClearShader;
        public List<TileBase> SimulateAtmoTiles;

        private int _adventMain, _buoyancyMain, _impulseMain, _divergenceMain, _jacobiMain, _subtractMain, _clearMain;

        private Tilemap _tilemap;

        private BoundsInt _mapBounds;
        private Vector2Int _mapSize;
        private int _linearMapSize;

        private ComputeBuffer _walls, _divergence;
        private RenderTextureRotating _velocity, _densityTemp, _pressure;

        private void Start()
        {
            _adventMain = AdventShader.FindKernel("Advent");
            _buoyancyMain = BuoyancyShader.FindKernel("Buoyancy");
            _impulseMain = ImpulseShader.FindKernel("Impulse");
            _divergenceMain = DivergenceShader.FindKernel("Divergence");
            _jacobiMain = JacobiShader.FindKernel("Jacobi");
            _subtractMain = SubtractGradientShader.FindKernel("Subtract");
            _clearMain = ClearShader.FindKernel("ClearBuffer");

            _tilemap = TileMapObject.GetComponent<Tilemap>();
            _mapBounds = _tilemap.cellBounds;
            _mapSize = new Vector2Int(_mapBounds.size.x, _mapBounds.size.y);

            // Calculate obstacle locations
            var walls = new float4[_mapSize.x, _mapSize.y];
            foreach (var position in _mapBounds.allPositionsWithin)
            {
                walls[position.x - _mapBounds.xMin, position.y - _mapBounds.yMin] = new float4(100);
                if (position.x == _mapBounds.xMin || position.x == _mapBounds.xMax || position.y == _mapBounds.yMin
                    || position.y == _mapBounds.yMax)
                    continue; // Even if border tile is a simulate atmo tile, it will be defined as a wall.
                if (SimulateAtmoTiles.Contains(_tilemap.GetTile(position)))
                    walls[position.x - _mapBounds.xMin, position.y - _mapBounds.yMin] = new float4(-100);
            }

            _linearMapSize = _mapSize.x * _mapSize.y;
            _velocity = new RenderTextureRotating(_linearMapSize, ComputeBufferMode.Dynamic);
            _densityTemp = new RenderTextureRotating(_linearMapSize);
            _pressure = new RenderTextureRotating(_linearMapSize);
            
            _walls = new ComputeBuffer(_linearMapSize, 4 * sizeof(float), ComputeBufferType.Structured,ComputeBufferMode.SubUpdates);
            _walls.SetData(walls);
            _divergence = new ComputeBuffer(_linearMapSize, 4 * sizeof(float), ComputeBufferType.Structured,ComputeBufferMode.SubUpdates);
        }

        private void Advent(ComputeBuffer current, ComputeBuffer past, float timeStep)
        {
            AdventShader.SetFloat("TimeStep", timeStep);
            // DEBUG FRICTION!
            AdventShader.SetFloat("Dissipation", 0.99f);
            AdventShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            
            AdventShader.SetBuffer(_adventMain,"Current", current);
            AdventShader.SetBuffer(_adventMain,"Past", past);
            AdventShader.SetBuffer(_adventMain,"Velocity", _velocity.Past);
            AdventShader.SetBuffer(_adventMain,"Walls", _walls);
            
            AdventShader.Dispatch(_adventMain, _linearMapSize,//(int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void ApplyBuoyancy(ComputeBuffer currentVelocity, ComputeBuffer densityTemp, ComputeBuffer pastVelocity, float timeStep)
        {
            
            BuoyancyShader.SetFloat("TimeStep", timeStep);
            AdventShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            // DEBUG VALUES!
            BuoyancyShader.SetFloat("AmbientTemperature", 0f);
            BuoyancyShader.SetFloat("Sigma", 1f); // Smoke buoyancy.
            BuoyancyShader.SetFloat("Kappa", 0.05f); // Smoke mass.
            
            BuoyancyShader.SetBuffer(_buoyancyMain,"CurrentVelocity", currentVelocity);
            BuoyancyShader.SetBuffer(_buoyancyMain,"PastVelocity", pastVelocity);
            BuoyancyShader.SetBuffer(_buoyancyMain,"DensityTemp", densityTemp);
            
            BuoyancyShader.Dispatch(_buoyancyMain, (int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void ApplyImpulseToDensityTemp(Vector2 position, float radius)
        {
            ImpulseShader.SetFloat("Radius", radius);
            ImpulseShader.SetFloats("Point", position.x, position.y);
            ImpulseShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            // DEBUG VALUES!
            ImpulseShader.SetFloats("Fill", 1f, 10f); // Density and Temp fill respectively.
            
            ImpulseShader.SetBuffer(_impulseMain,"Current", _densityTemp.Current);
            ImpulseShader.SetBuffer(_impulseMain,"Past", _densityTemp.Past);
            
            ImpulseShader.Dispatch(_impulseMain, (int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void ComputeDivergence(ComputeBuffer divergence, ComputeBuffer pastVelocity)
        {
            DivergenceShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            
            DivergenceShader.SetBuffer(_divergenceMain,"CurrentDivergence", divergence);
            DivergenceShader.SetBuffer(_divergenceMain,"PastVelocity", pastVelocity);
            DivergenceShader.SetBuffer(_divergenceMain,"Walls", _walls);
            
            DivergenceShader.Dispatch(_divergenceMain, (int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void ClearBuffer(ComputeBuffer buffer)
        {
            ClearShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            
            ClearShader.SetBuffer(_clearMain,"TargetBuffer", buffer);
            
            ClearShader.Dispatch(_clearMain, (int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void Jacobi(ComputeBuffer currentPressure, ComputeBuffer pastPressure, ComputeBuffer divergence)
        {
            JacobiShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            // DEBUG VALUES
            JacobiShader.SetFloat("Alpha", -1);
            JacobiShader.SetFloat("InverseBeta", 0.25f);
            
            JacobiShader.SetBuffer(_jacobiMain,"CurrentPressure", currentPressure);
            JacobiShader.SetBuffer(_jacobiMain,"PastPressure", pastPressure);
            JacobiShader.SetBuffer(_jacobiMain,"Divergence", divergence);
            JacobiShader.SetBuffer(_jacobiMain,"Walls", _walls);
            
            JacobiShader.Dispatch(_jacobiMain, (int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void SubtractGradient(ComputeBuffer currentVelocity, ComputeBuffer pastPressure, ComputeBuffer pastVelocity)
        {
            SubtractGradientShader.SetInts("MapSize", _mapSize.x, _mapSize.y);
            // DEBUG VALUES
            SubtractGradientShader.SetFloat("GradientScale", 1);
            
            SubtractGradientShader.SetBuffer(_subtractMain,"CurrentVelocity", currentVelocity);
            SubtractGradientShader.SetBuffer(_subtractMain,"PastPressure", pastPressure);
            SubtractGradientShader.SetBuffer(_subtractMain,"PastVelocity", pastVelocity);
            SubtractGradientShader.SetBuffer(_subtractMain,"Walls", _walls);
            
            SubtractGradientShader.Dispatch(_subtractMain, (int) math.ceil(_linearMapSize / 64f),
                1, 1);
        }

        private void FixedUpdate()
        {
            // Check here for any wall changes.
            // DOES NOT WORK. No clue why the compute buffers are not reading and writing correctly. Oh well.
            // Good education though. I now understand what it's doing.

            var timeStep = Time.deltaTime * 1;
            
            // Advect velocity against its self
            Advent(_velocity.Current, _velocity.Past, timeStep);
            // Advect temperature and density against velocity
            Advent(_densityTemp.Current, _densityTemp.Past, timeStep);
            
            _velocity.Swap();
            _densityTemp.Swap();
            
            Debug.Log("READ?");
            Debug.Break();
            
            // Apply forces here.
            ApplyBuoyancy(_velocity.Current, _densityTemp.Past, _velocity.Past, timeStep);
            
            _velocity.Swap();
            
            // Refresh the impulse of density and temperature
            ApplyImpulseToDensityTemp(new Vector2(0, 28), 4);
            
            _densityTemp.Swap();

            // Calculates how divergent the velocity is
            ComputeDivergence(_divergence, _velocity.Past);
            
            // Clearing pressure pressure for Jacobi loops.
            ClearBuffer(_pressure.Past);
            
            for (var i = 0; i < 50; ++i)
            {
                Jacobi(_pressure.Current, _pressure.Past, _divergence);
                _pressure.Swap();
            }
            
            SubtractGradient(_velocity.Current, _pressure.Past, _velocity.Past);
            _velocity.Swap();
        }

        private void OnDestroy()
        {
            _velocity.Dispose();
            _densityTemp.Dispose();
            _pressure.Dispose();
            _walls.Dispose();
            _divergence.Dispose();
        }
    }
}
