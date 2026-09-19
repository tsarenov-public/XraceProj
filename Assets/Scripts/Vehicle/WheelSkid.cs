using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(WheelCollider))]
public class WheelSkid : MonoBehaviour {

	public AudioSource SkidSound;
	public float SkidSoundMultiplyer;

	[SerializeField]
	Rigidbody rb;
	[SerializeField]
	Skidmarks skidmarksController;


	WheelCollider wheelCollider;
	WheelHit wheelHitInfo;

	const float SKID_FX_SPEED = 1.2f;        // мин. боковое скольжение для следа
	const float MAX_SKID_INTENSITY = 20.0f;  // скорость, при которой след максимальной черноты
	const float WHEEL_SLIP_MULTIPLIER = 5.0f; // вклад пробуксовки в след
	int lastSkid = -1;
	float lastFixedUpdateTime;

    protected void Awake() {
		wheelCollider = GetComponent<WheelCollider>();
		lastFixedUpdateTime = Time.time;
	}

	protected void FixedUpdate() {
		lastFixedUpdateTime = Time.time;
	}

	protected void LateUpdate() {
		if (wheelCollider.GetGroundHit(out wheelHitInfo))
		{
			// Боковое скольжение
			Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
			float skidTotal = Mathf.Abs(localVelocity.x);

			// Пробуксовка — только при движении вперёд 
			float carForwardVel = Vector3.Dot(rb.linearVelocity, transform.forward);
			float wheelSpin = 0f;

			if (carForwardVel > 0.5f)
			{
				float wheelAngularVelocity = wheelCollider.radius * ((2 * Mathf.PI * wheelCollider.rpm) / 60);
				wheelSpin = Mathf.Abs(carForwardVel - wheelAngularVelocity) * WHEEL_SLIP_MULTIPLIER;
				wheelSpin = Mathf.Max(0, wheelSpin * (6 - carForwardVel));
			}

			skidTotal += wheelSpin;

			// Рисуем след и даём визг, если скольжение настоящее
			if (skidTotal >= SKID_FX_SPEED) {
				float intensity = Mathf.Clamp01(skidTotal / MAX_SKID_INTENSITY);
				Vector3 skidPoint = wheelHitInfo.point + (rb.linearVelocity * (Time.time - lastFixedUpdateTime));
				lastSkid = skidmarksController.AddSkidMark(skidPoint, wheelHitInfo.normal, intensity, lastSkid);

				SkidSound.volume = intensity / SkidSoundMultiplyer;
			}
			else
			{
				lastSkid = -1;
				SkidSound.volume = 0f;
			}
		}
		else
		{
			lastSkid = -1;
			SkidSound.volume = 0f;
		}
	}
}