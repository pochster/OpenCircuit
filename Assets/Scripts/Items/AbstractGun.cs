﻿using UnityEngine;
using System.Collections;

using UnityEngine.Networking;

public abstract class AbstractGun : Item {

	public AudioClip fireSound;
	public float fireSoundVolume = 1;
	public float fireDelay = 0.1f;

	public float reloadTime = 1f;
	public int magazineSize = 20;
	public int maxMagazines = 5;

	public float baseInaccuracy = 0.1f;
	public float maximumMovementInaccuracy = 0.1f;
	public float movementInaccuracySoftness = 10f;
	public Vector3 recoilAnimationDistance = new Vector3(0, 0, 0.2f);
	public Vector2 recoilMinRotation = new Vector3(-0.1f, 0.1f);
	public Vector2 recoilMaxRotation = new Vector3(0.1f, 0.2f);

	public Vector3 fireEffectLocation;
	public EffectSpec fireEffect;
	public EffectSpec fireEffectSideways;
	public EffectSpec fireEffectLight;

	public AudioClip reloadSound;

	protected int maxBullets;
	[SyncVar]
	protected int bulletsRemaining = 5 * 20;
	[SyncVar]
	protected int currentMagazineFill;
	
	protected AudioSource audioSource;
	protected Inventory inventory;


	protected bool shooting = false;
	protected bool reloading = false;

	protected float cycleTime = 0;
	protected float reloadTimeRemaining = 0;

	private AudioSource soundEmitter;

	void Start() {
		soundEmitter = gameObject.AddComponent<AudioSource>();
		maxBullets = magazineSize * maxMagazines;
		currentMagazineFill = magazineSize; //one mag loaded
		bulletsRemaining = (maxMagazines - 1) * magazineSize; //the rest in reserve
	}

	[ClientCallback]
	public override void Update() {
		base.Update();
		if(cycleTime > 0)
			cycleTime -= Time.deltaTime;
		if(cycleTime <= 0 && shooting && !reloading) {
			Transform cam = inventory.getPlayer().cam.transform;
			shoot(cam.position, cam.forward);
			MouseLook looker = inventory.getPlayer().looker;
			looker.rotate(
				Random.Range(recoilMinRotation.x, recoilMaxRotation.x),
				Random.Range(recoilMinRotation.y, recoilMaxRotation.y));
		} else if(reloading) {
			reloadTimeRemaining -= Time.deltaTime;
			if(reloadTimeRemaining <= 0) {
				reloading = false;
			}
		}
	}

	public override void beginInvoke(Inventory invoker) {
		this.inventory = invoker;
		shooting = true;
	}

	public override void endInvoke(Inventory invoker) {
		base.endInvoke(invoker);
		shooting = false;
	}

	public override void onEquip(Inventory equipper) {
		base.onEquip(equipper);
		audioSource = equipper.gameObject.AddComponent<AudioSource>();
		audioSource.clip = fireSound;
		audioSource.volume = fireSoundVolume;
	}

	public override void onUnequip(Inventory equipper) {
		base.onUnequip(equipper);
		if(audioSource != null)
			Destroy(audioSource);
	}

	public bool addMags(int number) {
		int currentAmmo = bulletsRemaining + currentMagazineFill;
		if(currentAmmo < maxBullets) {
			int bulletsNeeded = maxBullets - currentAmmo;
			bulletsRemaining += (bulletsNeeded < magazineSize * number) ? bulletsNeeded : magazineSize * number;
			return true;
		} else {
			return false;
		}

	}

	public void reload() {
		if(currentMagazineFill < magazineSize) {
			reloading = true;
			soundEmitter.PlayOneShot(reloadSound);
			int bulletsNeeded = magazineSize - currentMagazineFill;
			if(bulletsRemaining > bulletsNeeded) {
				currentMagazineFill += bulletsNeeded;
				bulletsRemaining -= bulletsNeeded;
			} else {
				currentMagazineFill += bulletsRemaining;
				bulletsRemaining = 0;
			}
			reloadTimeRemaining += reloadTime;
		}
	}

	protected void shoot(Vector3 position, Vector3 direction) {
		if(currentMagazineFill > 0) {
			audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
			audioSource.Play();
			direction = inaccurateDirection(direction, getMovementInaccuracy());
            direction = inaccurateDirection(direction, baseInaccuracy);
            doBullet(position, direction, 1);
			cycleTime += fireDelay;
			--currentMagazineFill;
			transform.position -= transform.TransformVector(recoilAnimationDistance);
			
			// do fire effects
			Vector3 effectPosition = transform.TransformPoint(fireEffectLocation);
			fireEffect.spawn(effectPosition, -transform.forward);
			fireEffectSideways.spawn(effectPosition, -transform.right -transform.forward);
			fireEffectSideways.spawn(effectPosition, transform.right -transform.forward);
			fireEffectLight.spawn(effectPosition);
		} else {
			reload();
		}
	}

	protected virtual float getMovementInaccuracy() {
		// here we use a rational function to get the desired behaviour
		const float arbitraryValue = 0.2f; // the larger this value is, the faster the player must be moving before it affects his accuracy
		float speed = inventory.GetComponent<Rigidbody>().velocity.magnitude;
		float inaccuracy = (maximumMovementInaccuracy * speed -arbitraryValue) / (speed +movementInaccuracySoftness);
		return Mathf.Max(inaccuracy, 0);
	}

	protected abstract void doBullet(Vector3 position, Vector3 direction, float power);

	public static Vector3 inaccurateDirection(Vector3 direction, float inaccuracy) {
		Vector3 randomAngle = Random.onUnitSphere;
		float angle = Vector3.Angle(direction, randomAngle) /360;
		return Vector3.RotateTowards(direction, Random.onUnitSphere, Mathf.PI *angle *inaccuracy, 0);
	}

}
