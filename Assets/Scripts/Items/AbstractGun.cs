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
	public Vector3 recoilDistance = new Vector3(0, 0, 0.2f);


	protected int maxBullets;
	protected int bulletsRemaining = 5 * 20;
	protected int currentMagazineFill;
	
	protected AudioSource audioSource;
	protected Inventory inventory;


	protected bool shooting = false;
	protected bool reloading = false;

	protected float cycleTime = 0;
	protected float reloadTimeRemaining = 0;

	void Start() {
		maxBullets = magazineSize * maxMagazines;
		currentMagazineFill = magazineSize; //one mag loaded
		bulletsRemaining = (maxMagazines - 1) * magazineSize; //the rest in reserve
	}

	[ClientCallback]
	void Update() {
		base.Update();
		if(cycleTime > 0)
			cycleTime -= Time.deltaTime;
		if(cycleTime <= 0 && shooting && !reloading) {
			Transform cam = inventory.getPlayer().cam.transform;
			shoot(cam.position, cam.forward);
		} else if(reloading) {
			print("reloading");
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

	protected void shoot(Vector3 position, Vector3 direction) {
		print(currentMagazineFill + " bullets in mag");
		if(currentMagazineFill > 0) {
			audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
			audioSource.Play();
			doBullet(position, direction, 1);
			cycleTime += fireDelay;
			--currentMagazineFill;
			transform.position -= transform.TransformVector(recoilDistance);
		} else {
			reloading = true;
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

	protected abstract void doBullet(Vector3 position, Vector3 direction, float power);

}
