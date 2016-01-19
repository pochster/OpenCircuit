﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Scripts/Player/Parkour")]
public class Parkour : MovementController {

	private Player myPlayer;
	private Vector3 groundNormal;
	private Vector3 groundSpeed;
	private Vector3 navPoint;
	private float forwardSpeed = 0;
	private float rightSpeed = 0;
	//private bool grounded;
	private bool sprinting = false;
	private bool crouching = false;
	private bool autoMove = false;
	private GameObject floor = null;
	private CapsuleCollider col;
	private AudioSource footstepEmitter;
	private float nextFootstep;

	// climbing stuff
	//private float minStepHeight = 0.1f;
	private float maxStepHeight = 1f;
	//private float minLedgeWidth = 0.25f;
	private float normHeight;
	private int freeFallDelay = 0;
	private bool canMove = true;
	private Vector3 lastRelativeVelocity = Vector3.zero;
	private int collisionCount = 0;
	private List<float> pastForces = new List<float>();
	private List<int> pastForcesAddedCount = new List<int>();
	private int pastForceCount = 5;
	private int lastForceCount = 0;

	public float sprintMult = 2f;
	public float walkSpeedf = 6f;
	public float walkSpeedb = 4f;
	public float walkSpeedr = 4f;
	public float walkSpeedl = 4f;
	public float acceleration = 0.2f;
	public float climbRate = 0.1f;
	public float crouchSpeedMultiplier = 0.5f;
	public float minCrouchHeight = 1;
	public float crouchHeight = 1.25f;
	public float jumpSpeed = 5;
	public float oxygenStopSprint = 10;
	public float oxygenBeginSprint = 15;
	public float oxygenSprintUsage = 30;
	public AudioClip[] footsteps;
	public float minimumFoostepOccurence;
	public float foostepSpeedScale;
	public float fallHurtSpeed = 10;
	public float fallDeathSpeed = 15;

	void Awake() {
		groundNormal = Vector3.zero;
		groundSpeed = Vector3.zero;
		myPlayer = GetComponent<Player>();
		col = GetComponent<CapsuleCollider>();
		normHeight = col.height;
		footstepEmitter = gameObject.AddComponent<AudioSource>();
		footstepEmitter.enabled = true;
		footstepEmitter.loop = false;
		nextFootstep = 0;
	}

	void FixedUpdate() {
		// change collider height
		//float desiredHeight = crouching? crouchHeight: normHeight;
		float desiredHeight = crouchHeight;
		if (col.height < desiredHeight) {
			if (freeFallDelay == 0)
				setColliderHeight(col.height + climbRate /5);
			else
				setColliderHeight(col.height + climbRate);
			if (col.height > desiredHeight)
				setColliderHeight(desiredHeight);
		} else if (col.height > desiredHeight) {
			setColliderHeight(col.height - climbRate);
			if (col.height < desiredHeight)
				setColliderHeight(desiredHeight);
		}

		//check if grounded
		float dHeight = crouching ? 0.25f : 0.65f;
		float height = doGroundedCheck();
		if (isGrounded()) {
			if (height < dHeight) {
				Vector3 vel = GetComponent<Rigidbody>().velocity;
				vel.y += Mathf.Max(-acceleration*2, Mathf.Min(acceleration, (dHeight -height) *20 -vel.y));
				GetComponent<Rigidbody>().velocity = vel;
				//Vector3 position = transform.position;
				//position.y += Mathf.Min(0.1f, dHeight -height);
				//transform.position = position;
			}
		}

		// update past forces
		int newForceCount = pastForces.Count -lastForceCount;
		pastForcesAddedCount.Add(newForceCount);
		if (pastForcesAddedCount.Count > pastForceCount) {
			int forcesToRemove = pastForcesAddedCount[0];
			pastForcesAddedCount.RemoveAt(0);
			pastForces.RemoveRange(0, forcesToRemove);
		}
		for (int i = 0; i < pastForces.Count; ++i)
			pastForces[i] *= 0.8f;
		lastForceCount = pastForces.Count;

		// move the player
		if (freeFallDelay < 0) {
			++freeFallDelay;
			return;
		}
		if (!isGrounded()) {
			if (freeFallDelay > 0)
				--freeFallDelay;
			else {
				nextFootstep = 1;
				return;
			}
		} else
			freeFallDelay = 2;

		// get the move direction and speed
		if (!canMove) {
			rightSpeed = 0;
			forwardSpeed = 0;
		}
		Vector3 desiredVel = getDesiredVel();

		float speed = desiredVel.magnitude * (sprinting ? calculateSprintMultiplier() : 1) *(crouching ? crouchSpeedMultiplier : 1);
		desiredVel.y = 0;
		desiredVel.Normalize();

		// move parallel to the ground
		if (isGrounded()) {
			Vector3 sideways = Vector3.Cross(Vector3.up, desiredVel);
			desiredVel = Vector3.Cross(sideways, groundNormal).normalized;
		}
		desiredVel *= speed;

		// slow down when moving up a slope
		if (desiredVel.y > 0)
			desiredVel *= 1 - Mathf.Pow(desiredVel.y / speed, 2);
		if (!isGrounded())
			desiredVel.y = GetComponent<Rigidbody>().velocity.y;

		// handle player not being able to "increase" gravity
		if (GetComponent<Rigidbody>().velocity.y > desiredVel.y)
			desiredVel.y = GetComponent<Rigidbody>().velocity.y;

		if (isGrounded()) {
			if (height > dHeight)
				desiredVel.y = GetComponent<Rigidbody>().velocity.y;
		}

		// handle the maximum acceleration
		Vector3 force = desiredVel -GetComponent<Rigidbody>().velocity +groundSpeed;
		float maxAccel = acceleration;// Mathf.Min(Mathf.Max(acceleration *force.magnitude, acceleration /3), acceleration *2);
		if (force.magnitude > maxAccel) {
			force.Normalize();
			force *= maxAccel;
		}
		GetComponent<Rigidbody>().AddForce(force, ForceMode.VelocityChange);

		// play footstep sounds
		float currentSpeed = GetComponent<Rigidbody>().velocity.sqrMagnitude;
		if (nextFootstep <= Time.fixedTime && currentSpeed > 0.1f) {
			if (nextFootstep != 0) {
				float volume = 0.8f -(0.8f / (1 + currentSpeed /100));
				playFootstep(volume);
				LabelHandle audioLabel = new LabelHandle(transform.position, "footsteps");
				audioLabel.addTag(new Tag(TagEnum.Sound, volume));
				audioLabel.addTag(new Tag(TagEnum.Threat, 5f));
				AudioEvent footStepsEvent = new AudioEvent(transform.position, audioLabel, transform.position);
				footStepsEvent.broadcast(volume);
			}
			nextFootstep = Time.fixedTime + minimumFoostepOccurence / (1 + currentSpeed * foostepSpeedScale);
		}
		if (desiredVel.sqrMagnitude == 0 && currentSpeed < 1f && nextFootstep != 0) {
			float volume = 0.1f * currentSpeed;
			playFootstep(volume);
			nextFootstep = 0;
		}


		// update sprinting
		if (desiredVel.sqrMagnitude > 0.1f && sprinting) {
			if (myPlayer.oxygen < oxygenStopSprint)
				myPlayer.oxygen -= myPlayer.oxygenRecoveryRate *Time.deltaTime;
			else if (crouching)
				sprinting = false;
			else
				myPlayer.oxygen -= oxygenSprintUsage * Time.deltaTime;
		}
	}

	public float minDist = .1f;
	private Vector3 getDesiredVel() {
		Vector3 desiredVel;
		if (autoMove) {
			Vector3 dir = this.navPoint -transform.position;
			dir.y = 0;
			if (dir.magnitude < minDist) {
				desiredVel = Vector3.zero;
				autoMove = false;
			} else {
				desiredVel = dir.normalized * walkSpeedf;
			}
		} else {
			desiredVel = new Vector3(rightSpeed, 0, forwardSpeed);
			desiredVel = myPlayer.cam.transform.TransformDirection(desiredVel);
		}
		return desiredVel;
	}

	protected float calculateSprintMultiplier() {
		if (myPlayer.oxygen < oxygenStopSprint)
			return (sprintMult -1) *(myPlayer.oxygenRecoveryRate /oxygenSprintUsage) + 1;
		return sprintMult;
	}

	private void playFootstep(float volume) {
		AudioClip clip = footsteps[Random.Range(0, footsteps.Length)];
		footstepEmitter.pitch = 0.9f +0.2f *Random.value;
		footstepEmitter.PlayOneShot(clip, volume);
	}

	void OnCollisionEnter(Collision collisionInfo) {
		++collisionCount;
		doFallDamage(collisionInfo);
	}

	void OnCollisionStay(Collision collisionInfo) {
		doFallDamage(collisionInfo);
	}

	void OnCollisionExit(Collision collisionInfo) {
		--collisionCount;

	}

	protected void doFallDamage(Collision collisionInfo) {
		lastRelativeVelocity = collisionInfo.relativeVelocity;
		lastRelativeVelocity.y = Mathf.Clamp(lastRelativeVelocity.y, -walkSpeedf, walkSpeedf); // to represent that the player can't run on walls.
		lastRelativeVelocity.z = Mathf.Clamp(lastRelativeVelocity.z, -walkSpeedf *sprintMult *2, walkSpeedf * sprintMult *2); // to represent that the player can't run on walls.
		lastRelativeVelocity.x = Mathf.Clamp(lastRelativeVelocity.x, -walkSpeedf * sprintMult *2, walkSpeedf * sprintMult *2); // to represent that the player can't run on walls.
		Vector3 relativeVelocity = collisionInfo.relativeVelocity - lastRelativeVelocity;
		float relativeSpeed = Mathf.Max(relativeVelocity.magnitude, 0.01f);
		float impulse = Mathf.Max(collisionInfo.impulse.magnitude, 0.01f);
		float collisionSpeed = Mathf.Sqrt(relativeSpeed * impulse * (1 - Mathf.Abs(Vector3.Dot(relativeVelocity / relativeSpeed, collisionInfo.impulse / impulse)))) + impulse;
		if (collisionSpeed > fallHurtSpeed / 8f) {
			//print("Speed: " + relativeSpeed +", Impulse: " + impulse +", Combined: " + collisionSpeed);
			pastForces.Add(collisionSpeed);

			float forceSum = 0;
			for (int i = 0; i < pastForces.Count; ++i) {
				forceSum += pastForces[i];
				//pastForces[i] *= 0.9f;
			}
			float averageForce = forceSum + collisionSpeed;
			if (averageForce > 5)
				//print("Force: " + averageForce);
				if (averageForce > fallHurtSpeed && collisionSpeed > fallHurtSpeed / 2f) {
					float damage = (averageForce - fallHurtSpeed) / (fallDeathSpeed - fallHurtSpeed);
					myPlayer.hurt(damage * myPlayer.maxSuffering);
					//pastForces[pastForces.Count - 1] *= 0.5f;
				}
		}
	}

	public override void setForward(float percent) {
		if (percent > 0)
			forwardSpeed = percent *walkSpeedf;
		else
			forwardSpeed = percent *walkSpeedb;
	}

	public override void setRight(float percent) {
		if (percent > 0)
			rightSpeed = percent *walkSpeedr;
		else
			rightSpeed = percent *walkSpeedl;
	}

	public override void setSprinting(bool sprint) {
		//if (recovering && myPlayer.oxygen < oxygenBeginSprint)
		//	return;
		sprinting = sprint;
	}

	public override void setCrouching(bool crouch) {
		crouching = crouch;
	}

	public override void jump() {
		if (!canMove) return;
		if (freeFallDelay == 0 && !isGrounded())
			return;
		freeFallDelay = 0;
		Vector3 upSpeed = new Vector3(0, jumpSpeed, 0);
		if (floor != null && floor.GetComponent<Rigidbody>() != null) upSpeed.y += floor.GetComponent<Rigidbody>().velocity.y;
		GetComponent<Rigidbody>().velocity = GetComponent<Rigidbody>().velocity + upSpeed;
		floor = null;
		setColliderHeight(normHeight - maxStepHeight);
	}

	private void setColliderHeight(float h) {
		if (h < col.radius * 2) h = col.radius * 2;
		col.center = col.center + new Vector3(0, (col.height - h) / 2, 0);
		col.height = h;
	}

	public bool isGrounded() {
		return (floor != null && groundNormal.y > 0.6f) || (GetComponent<Rigidbody>().IsSleeping());
	}

	public override void lockMovement() {
		canMove = false;
	}

	public override void unlockMovement() {
		canMove = true;
	}

	public override void moveToPoint(Vector3 point) {
		navPoint = point;
		autoMove = true;
	}

	public override bool isAutoMoving() {
		return autoMove;
	}

	protected Vector3 calculateAcceleration(Vector3 desiredVelocity) {
		return Vector3.one;
	}

	protected float doGroundedCheck() {
		RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.35f, -Vector3.up, 1f);
		float distance = float.PositiveInfinity;
		foreach(RaycastHit hit in hits) {
			if (distance > hit.distance && hit.collider.gameObject != gameObject) {
				distance = hit.distance;
				groundNormal = hit.normal;
				floor = hit.collider.gameObject;
			}
		}
		bool grounded = distance < float.PositiveInfinity;
		if (!grounded) {
			floor = null;
			groundNormal = Vector3.zero;
		} else {
			print(groundNormal);
		}
		return distance;
    }



	/////////////////////
	//  Climbing Code  //
	/////////////////////
	protected Vector3 getCapsuleStart() {
		return transform.position +Vector3.up *3;
	}

	protected Vector3 getCapsuleEnd() {
		return getCapsuleStart() + new Vector3(0, 2f -0.8f, 0);
	}

	protected Vector3 getDirection() {
		Vector3 ledgeDirection = myPlayer.cam.transform.forward;
		ledgeDirection.y = 0;
		return ledgeDirection.normalized;
	}

#if UNITY_EDITOR

	public void OnGUI() {
		GUI.Label(new Rect(100, 10, 100, 20), "Grounded: " +isGrounded());
		GUI.Label(new Rect(100, 30, 100, 20), "Speed: " +GetComponent<Rigidbody>().velocity.magnitude.ToString());
	}

	public void OnDrawGizmosSelected() {
		if (!Application.isPlaying)
			return;

		RaycastHit hitInfo;
		bool ledgeExists = Physics.SphereCast(getCapsuleStart() +getDirection() *1f, 0.4f, -Vector3.up, out hitInfo, 2.5f);

		float dropDistance = ledgeExists ? hitInfo.distance -0.05f : 0;
		//Gizmos.DrawWireSphere(getCapsuleStart(), 0.4f);
		//Gizmos.DrawWireSphere(getCapsuleEnd(), 0.4f);
		bool ledgeFree = false;
		if (ledgeExists)
			ledgeFree = !Physics.CapsuleCast(getCapsuleStart() -Vector3.up *dropDistance, getCapsuleEnd() -Vector3.up *dropDistance, 0.4f, getDirection(), 1);

		Gizmos.color = ledgeFree ? Color.blue : Color.red;
		Gizmos.DrawWireSphere(getCapsuleStart() +getDirection() *1f -Vector3.up *dropDistance, 0.4f);
		Gizmos.DrawWireSphere(getCapsuleEnd() +getDirection() *1f -Vector3.up *dropDistance, 0.4f);
	}
#endif
}