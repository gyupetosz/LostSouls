using UnityEngine;

namespace LostSouls.Objects
{
    public class SpinObject : MonoBehaviour
    {
        [SerializeField] private float rotateSpeed = 90f; // degrees per second

        private void Update()
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }
}
