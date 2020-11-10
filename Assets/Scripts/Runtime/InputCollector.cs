using UnityEngine;

namespace Runtime
{
    public class InputCollector : MonoBehaviour
    {
        private Rigidbody2D _playerRigidbody;

        private void Start()
        {
            _playerRigidbody = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            // Get the keyboard input data
            var inputAxis = new Vector2();
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                inputAxis.y = 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                inputAxis.y = -1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                inputAxis.x = -1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                inputAxis.x = 1;

            // Get the touch input data
            // if (input.TouchCount() > 0 && input.GetTouch(0).phase == TouchState.Moved)
            // {
            //     var touchDelta = new float2(input.GetTouch(0).deltaX, input.GetTouch(0).deltaY);
            //     inputAxis = math.normalizesafe(touchDelta);
            // }

            _playerRigidbody.AddForce(inputAxis.normalized * 10);
        }
    }
}