using UnityEngine;
using UnityEngine.UIElements;


public class HasarImleci : MonoBehaviour

{
    public RectTransform comp;

    // eyw gpt adamsýn ama kaç saat sürdü bu cevabý verebilmen :<
    public void Init(Vector3 enemy, float time, Transform bizpos)
    {
        // Get direction from this player to the enemy in world space
        Vector3 toEnemy = (enemy - bizpos.position).normalized;

        // Convert that world-space direction into local space relative to this player's transform
        Vector3 localDir = bizpos.InverseTransformDirection(toEnemy);

        // Project that onto the XZ plane (because it's a compass)
        Vector2 localDir2D = new Vector2(localDir.x, localDir.z);

        // Calculate angle from the "needle's default direction" (right)
        float angle = Vector2.SignedAngle(Vector2.right, localDir2D);

        // Rotate compass needle UI (needle must point right by default)
        comp.localEulerAngles = new Vector3(0, 0, angle);


        Invoke(nameof(Yoket), time);
    }

    public void Yoket()
    {
        Destroy(gameObject);
    }
}
