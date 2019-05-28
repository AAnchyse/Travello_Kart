﻿using UnityEngine;
using System;
using UnityEngine.Events;
using Luminosity.IO;

public class WheelDrive : MonoBehaviour
{
	//Public 

	[Tooltip("Rigidbody of the vehicle.")]
	public Rigidbody rb;

	[Tooltip("Position of the center of mass.")]
	public Vector3 centerOfMass;	

    [Tooltip("Maximum steering angle of the wheels.")]
	public float maxAngle = 30f;

	[Tooltip("Minimum steering angle of the wheels.")]
	public float minAngle = 5f;

	[Tooltip("Maximum torque applied to the driving wheels.")]
	public float maxTorque = 300f;

	[Tooltip("Maximum torque applied to the driving wheels when reverse drive.")]
	public float reverseMaxTorque = 300f;

	[Tooltip("Maximum torque applied to the driving wheels.")]
	public float maxBoostTorque = 300f;

	[Tooltip("Maximum vehicle's speed (in m/s).")]
	public float maxSpeed =50;

	[Tooltip("Maximum vehicle's speed (in m/s).")]
	public static float maxBoostSpeed =40;

	[Tooltip("If you need the visual wheels to be attached automatically, drag the wheel shape here.")]
	public GameObject wheelMesh;

	[Tooltip("The vehicle's speed when the physics engine can use different amount of sub-steps (in m/s).")]
	public float criticalSpeed = 5f;

	[Tooltip("Simulation sub-steps when the speed is above critical.")]
	public int stepsBelow = 5;

	[Tooltip("Simulation sub-steps when the speed is below critical.")]
	public int stepsAbove = 1;

	[Tooltip("Threshold at which the boostGauge begins to fill")]
	public float slidingThreshold;

	[Tooltip("Coefficient which multiply the steering angle when drifting")]
	public float coeffAngleSteer;

	[Tooltip("Minimum time while not grounded to get haptic feedback")]
	public float minAirTime = 1;

	[Tooltip("Indicates if the 4 car's wheels are grounded.")]
	[HideInInspector]
	public static bool isGrounded;

	[Tooltip("Velocity of the rigidbody.")]
	[HideInInspector]
	public static float speed;

	[Tooltip("Indicates if bracking button is pushed.")]
	[HideInInspector]
	public static bool isBracking;

	[Tooltip("Indicates if we are skidding or not.")]
	[HideInInspector]
	public static bool isSkidding;

	[Tooltip("Boost duration after a drift.")]
	[HideInInspector]
	public static float boostGauge;

	[Tooltip("Event when car landed.")]
	[HideInInspector]
	public static UnityEvent landedEvent;

	[Tooltip("Friction curves used to change the stiffness of the wheels.")]
	public float stiffFrontForwardNormal;
	public float stiffFrontForwardDrift;
	public float stiffFrontSidewaysNormal;
	public float stiffFrontSidewaysDrift;
	public float stiffRearForwardNormal;
	public float stiffRearForwardDrift;
	public float stiffRearSidewaysNormal;
	public float stiffRearSidewaysDrift;

	//Private

	[Tooltip("List containing the 4 Wheel colliders.")]
    private WheelCollider[] m_Wheels;

	[Tooltip("Acceleration torque applied on wheels.")]
	private float torque;

	[Tooltip("Braking torque applied on wheels.")]
	private float handBrake;

	[Tooltip("Steering angle applied on front wheels.")]
	private float angle;

	[Tooltip("Friction curves used to change the stiffness of the wheels.")]
	private WheelFrictionCurve frictionCurveForward;
	private WheelFrictionCurve frictionCurveSideways;

	[Tooltip("Indicates if stiffness must be changed.")]
	private bool changeStiffness;

	[Tooltip("Acceleration applied on wheel torque, depending of rigidbody velocity.")]
	private float acc;

	[Tooltip("Indicates if we are drifting or not.")]
	private bool isDrifting;

	[Tooltip("Time spent when not grounded. Used for haptic feedback at landing")]
	private float airTime;


    // Find all the WheelColliders down in the hierarchy and instantiate wheel meshes
	void Start()
	{
		rb.centerOfMass = centerOfMass;

		m_Wheels = GetComponentsInChildren<WheelCollider>();

		m_Wheels[0].ConfigureVehicleSubsteps(criticalSpeed, stepsBelow, stepsAbove);

		for (int i = 0; i < m_Wheels.Length; ++i) 
		{
			var wheel = m_Wheels [i];

			if (wheelMesh != null)
			{
				var ws = Instantiate (wheelMesh);
				ws.transform.parent = wheel.transform;
			}
		}

		isDrifting = false;

		isBracking = false;

		airTime =0;

		landedEvent = new UnityEvent();
	}

	void ChangeFriction(WheelCollider wheel , float stiffnessForward , float stiffnessSideways)
	{
		frictionCurveForward = wheel.forwardFriction;
		frictionCurveSideways = wheel.sidewaysFriction;
		frictionCurveForward.stiffness = stiffnessForward;
		frictionCurveSideways.stiffness = stiffnessSideways;
		wheel.forwardFriction = frictionCurveForward;
		wheel.sidewaysFriction = frictionCurveSideways;
	}

	void Update()
	{	
		float angleInput = InputManager.GetAxis("Turn");

		bool accInput = InputManager.GetButton("Acceleration");

		bool brakeInput = InputManager.GetButton("Brake");

		bool driftInput = InputManager.GetButton("Drift");

		bool boostInput = InputManager.GetButton("Boost");

		handBrake = 0;

		torque = 0;

		speed = rb.velocity.magnitude;

		acc = (maxSpeed - speed) / maxSpeed;

		isBracking = false;

		isSkidding =false;

		//Acceleration
		if (accInput)
		{
			// Use boost
			if(boostInput||boostGauge>0)
			{
				if (boostGauge >0) boostGauge-= Time.deltaTime;

				if(speed < maxBoostSpeed )
				{
					torque =  maxBoostTorque *(maxBoostSpeed - speed) / maxBoostSpeed;
					handBrake = 0;
				}
				else
				{
					torque =  0;
					handBrake = maxBoostTorque;
				}
			}
			// Normal drive
			else
			{
				if(speed < maxSpeed )
				{
					torque =  maxTorque * acc;
					handBrake = 0;
				}
				else
				{
					torque =  0;
					handBrake = maxTorque;
				}
			}
		}

		// Braking or reverse drive
		else if (brakeInput)
		{
			isBracking = true;
			boostGauge = 0;
			
			// Braking
			if(transform.InverseTransformDirection(rb.velocity).z> 0) 
			{
				torque = 0;
				handBrake = Mathf.Infinity;
			}
			// Reverse drive
			else
			{
				torque = -reverseMaxTorque * acc;
				handBrake = 0;
			}
		}

		//Deceleration
		else
		{
			torque = 0;
			handBrake = maxTorque;
			boostGauge = 0;
		}
			
		// Change wheel stiffness
		changeStiffness = false;
		if((!isDrifting && driftInput)||(isDrifting && !driftInput ))
		{
			changeStiffness = true;
		}
		
		// Check if all wheels are grounded
		isGrounded = true;
		foreach (WheelCollider wheel in m_Wheels)
		{
			if (!wheel.GetGroundHit(out WheelHit hit))
			{
				isGrounded = false;
			}
		}

		// Move wheels
		foreach (WheelCollider wheel in m_Wheels)
		{
			if (isGrounded)
			{
				// Remove constraints
				rb.constraints = RigidbodyConstraints.None;

				if(airTime>minAirTime)
					landedEvent.Invoke();
				//Reset airTime
				airTime = 0;
				
				// Fill the boost gauge
				wheel.GetGroundHit(out WheelHit hit);
				if(isDrifting && Mathf.Abs(hit.sidewaysSlip)>slidingThreshold)
				{
					boostGauge +=Time.deltaTime/2;
					isSkidding =true;
				}

				// Front wheels
				if (wheel.transform.localPosition.z >= 0)
				{
					//Normal Steering
					angle = angleInput * (((minAngle - maxAngle)/maxBoostSpeed) * speed + maxAngle);

					//Drifting
					if (driftInput)
					{
						if(changeStiffness)
						{
							//Friction
							ChangeFriction(wheel , stiffFrontForwardDrift , stiffFrontSidewaysDrift);
							isDrifting =true;
						}

						//Drift Steering
						angle *=coeffAngleSteer; 
					}
					//Normal
					else
					{
						if (changeStiffness)
						{
							//Friction
							ChangeFriction(wheel , stiffFrontForwardNormal , stiffFrontSidewaysNormal);
							isDrifting =false;
						}
					}
					//Steering angle
					wheel.steerAngle = angle;
					
					//Torque
					wheel.motorTorque = torque;
				}

				//Rear wheels
				if (wheel.transform.localPosition.z < 0)
				{
					//Drifting
					if (driftInput)
					{
						if (changeStiffness)
						{
							//Friction
							ChangeFriction(wheel , stiffRearForwardDrift , stiffRearSidewaysDrift);
							isDrifting =true;
						}
					}
					//Normal
					else
					{
						if (changeStiffness)
						{
							//Friction
							ChangeFriction(wheel , stiffRearForwardNormal , stiffRearSidewaysNormal);
							isDrifting =false;
						}
					}
					//Braking
					wheel.brakeTorque = handBrake;

					//Torque
					wheel.motorTorque = torque;
				}
			}
			else
			{
				// Keep car direction while in air 
				rb.constraints = RigidbodyConstraints.FreezeRotationY;
				boostGauge = 0;
				airTime += Time.deltaTime;
			}

			// Update visual wheels 
			if (wheelMesh) 
			{
				Quaternion q;
				Vector3 p;
				wheel.GetWorldPose (out p, out q);

				// Assume that the only child of the wheelcollider is the wheel mesh
				Transform shapeTransform = wheel.transform.GetChild (0);
				shapeTransform.position = p;
				shapeTransform.rotation = q;
			}
		}
	}
}