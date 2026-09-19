using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelAlignment : MonoBehaviour {

    public GameObject wheelBase;
    public GameObject wheelGraphic;

    public WheelCollider wheelCol;

    public bool steerable;

    public float steeringAngle;

    RaycastHit hit;
	
	void Update ()
    {
        alignMeshToCollider();
	}

    void alignMeshToCollider()
    {
        if (Physics.Raycast(wheelCol.transform.position, -wheelCol.transform.up, out hit, wheelCol.suspensionDistance + wheelCol.radius))
        {
            wheelGraphic.transform.position = hit.point + wheelCol.transform.up *  wheelCol.radius;
        }
        else
        {
            wheelGraphic.transform.position = wheelCol.transform.position - (wheelCol.transform.up * wheelCol.suspensionDistance);
        }

        if (steerable)
        {
            wheelCol.steerAngle = steeringAngle;
        }

        wheelGraphic.transform.eulerAngles = new Vector3(wheelBase.transform.eulerAngles.x, wheelBase.transform.eulerAngles.y - wheelCol.steerAngle, wheelBase.transform.eulerAngles.z);
        wheelGraphic.transform.Rotate(wheelCol.rpm / 60 * 360 * Time.deltaTime, 0, 0);
    }
}
