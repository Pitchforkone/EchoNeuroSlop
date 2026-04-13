using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StepTypeZone : MonoBehaviour
{
    [SerializeField] private TypeOfStep _typeOfStep = TypeOfStep.Glass;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerWalkEcho playerWalkEcho))
        {
            playerWalkEcho.SetTypeOfStep(_typeOfStep);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerWalkEcho playerWalkEcho))
        {
            playerWalkEcho.RemoveTypeOfStep(_typeOfStep);
        }
    }
}
