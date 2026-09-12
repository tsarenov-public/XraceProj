using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelController : MonoBehaviour {

    public WheelAlignment[] steerableWheels;

    public float BreakPower;

    public float Horizontal;
    public float Vertical;

    //Steering variables
    public float wheelRotateSpeed;
    public float wheelSteeringAngle;

    //Motor variables
    public float wheelAcceleration;
    public float wheelMaxSpeed;

    [Header("Ручник (Space)")]
    public float handbrakeStiffness = 0.35f;  // боковое сцепление задних колёс на ручнике (меньше = скользче)
    public float handbrakeForce = 1500f;      // сила подтормаживания задних колёс

    public Rigidbody RB;

    void Update ()
    {
        wheelControl();
    }

    //Applies steering and motor torque
    void wheelControl()
    {
        bool handbrake = Input.GetKey(KeyCode.Space);

        for (int i = 0; i < steerableWheels.Length; i++)
        {
            WheelAlignment wheel = steerableWheels[i];

            //Sets default steering angle
            wheel.steeringAngle = Mathf.LerpAngle(wheel.steeringAngle, 0, Time.deltaTime * wheelRotateSpeed);
            //Sets default motor speed
            wheel.wheelCol.motorTorque = Mathf.Lerp(wheel.wheelCol.motorTorque, 0, Time.deltaTime * wheelAcceleration);

            //Motor controls
            Horizontal = Input.GetAxis("Horizontal");
            Vertical = Input.GetAxis("Vertical");

            if (Vertical > 0.1)
            {
                wheel.wheelCol.motorTorque = Mathf.Lerp(wheel.wheelCol.motorTorque, wheelMaxSpeed, Time.deltaTime * wheelAcceleration);
            }

            if (Vertical < -0.1)
            {
                wheel.wheelCol.motorTorque = -Mathf.Lerp(wheel.wheelCol.motorTorque, wheelMaxSpeed, Time.deltaTime * wheelAcceleration * BreakPower);
                RB.linearDamping = 0.3f;
            }
            else
            {
                RB.linearDamping = 0;
            }

            if (Horizontal > 0.1)
            {
                wheel.steeringAngle = Mathf.LerpAngle(wheel.steeringAngle, -wheelSteeringAngle, Time.deltaTime * wheelRotateSpeed);
            }

            if (Horizontal < -0.1)
            {
                wheel.steeringAngle = Mathf.LerpAngle(wheel.steeringAngle, wheelSteeringAngle, Time.deltaTime * wheelRotateSpeed);
            }

            // #### РУЧНИК: срываем заднюю ось ####
            if (handbrake && !wheel.steerable)
            {
                WheelFrictionCurve side = wheel.wheelCol.sidewaysFriction;
                side.stiffness = handbrakeStiffness;
                wheel.wheelCol.sidewaysFriction = side;
                wheel.wheelCol.brakeTorque = handbrakeForce;
            }
            else
            {
                WheelFrictionCurve side = wheel.wheelCol.sidewaysFriction;
                side.stiffness = 1f;
                wheel.wheelCol.sidewaysFriction = side;
                wheel.wheelCol.brakeTorque = 0f;
            }
        }
    }
}