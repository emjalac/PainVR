﻿using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PlayerController : NetworkBehaviour {

	public GameObject bulletPrefab;
	public Transform bulletSpawn;
	public Camera cam;
	public Canvas healthBar;
	public Text scoreText;
	public bool competing;
	public bool leftHanded;

	[SyncVar(hook = "OnChangeScore")]
	private int score;
	[SyncVar(hook = "OnChangeSpeed")]
	private float speed;
	[SyncVar(hook = "OnChangeDamage")]
	private int damage;
	[SyncVar(hook = "OnStun")]
	private bool stunned;
	private Vector3 maxPosition;
	private Vector3 minPosition;
	private bool firstTriggerPress;

	void Start()
	{
		score = 0;
		speed = 1.2f;
		stunned = false;
		damage = 10;
		var minObj = GameObject.FindGameObjectWithTag ("MinBoundary");
		var maxObj = GameObject.FindGameObjectWithTag ("MaxBoundary");
		minPosition = minObj.transform.position;
		maxPosition = maxObj.transform.position;
		firstTriggerPress = false;

		if (isLocalPlayer)
		{
			if (competing)
				scoreText.text = "My Score: " + score.ToString ();
			else
				scoreText.text = "Team Score: " + score.ToString ();
			if (leftHanded)
			{
				moveGun ();
			}
			return;
		}

		cam.enabled = false;
		healthBar.enabled = false;

		if (competing)
		{
			scoreText.transform.position = scoreText.transform.position + new Vector3 (0, 10, 0);
			scoreText.text = "Opponent's Score: " + score.ToString ();
		}
		else
			scoreText.enabled = false;
		if (leftHanded)
		{
			moveGun ();
		}
	}

	void moveGun()
	{
		Quaternion rot = gameObject.transform.rotation;
		transform.rotation = Quaternion.LookRotation (new Vector3 (0,0,1));
		GameObject gun = gameObject.transform.Find ("Main Camera/Gun").gameObject;
		GameObject bSpawn = gameObject.transform.Find ("Main Camera/Bullet Spawn").gameObject;
		Vector3 gunPos = gun.transform.position;
		Vector3 bSpawnPos = bSpawn.transform.position;
		gun.transform.position = new Vector3(gunPos.x - 1.0f, gunPos.y, gunPos.z);
		bulletSpawn.position = new Vector3(bSpawnPos.x - 1.0f, bSpawnPos.y, bSpawnPos.z);
		transform.rotation = rot;
	}

	// Use this for initialization
	public override void OnStartLocalPlayer()
	{
		GetComponent<MeshRenderer> ().material.color = Color.blue;
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (!isLocalPlayer)
		{
			return;
		}

		bool buttonPressed = false;
		OVRInput.Update ();
		if (!stunned)
		{
			var x = Input.GetAxis ("Horizontal") * Time.deltaTime * 150.0f;
			var z = Input.GetAxis ("Vertical") * Time.deltaTime * 3.0f * speed;

			transform.Rotate (0, x, 0);
			transform.Translate (0, 0, z);
			if (OutOfBounds())
				transform.Translate (0, 0, -z);

			if (Input.GetKeyDown (KeyCode.Space) || OVRInput.Get (OVRInput.RawAxis1D.LIndexTrigger) >= .95f
				|| OVRInput.Get (OVRInput.RawAxis1D.RIndexTrigger) >= .95f) {
				if (firstTriggerPress) {
					CmdFire ();
					firstTriggerPress = false;
					Invoke ("AllowRepeatFire", 0.4f);
				}
			}
			if (OVRInput.Get (OVRInput.RawAxis1D.LIndexTrigger) < .95f) {
				firstTriggerPress = true;
			}
		}
	}

	[Command] //this code is called on the client but runs on the server!
	void CmdFire()
	{
		// Create the Bullet from the Bullet Prefab
		var bullet = (GameObject)Instantiate (
			             bulletPrefab,
			             bulletSpawn.position,
			             bulletSpawn.rotation,
						 gameObject.transform);

		// Add velocity to the bullet
		bullet.GetComponent<Rigidbody>().velocity = bullet.transform.forward * 6;

		//Spawn the bullet on the clients
		NetworkServer.Spawn (bullet);

		// Destroy the bullet after 2 seconds
		Destroy(bullet, 2.0f);
	}

	public void IncrementScore()
	{
		score++;
	}

	public void IncrementTeamScore()
	{
		PlayerController[] players = FindObjectsOfType<PlayerController> ();
		foreach (PlayerController player in players)
		{
			player.IncrementScore ();
		}
	}

	void OnChangeScore(int new_score)
	{
		if (competing)
		{
			if (isLocalPlayer)
				scoreText.text = "My Score: " + new_score.ToString ();
			else
				scoreText.text = "Opponent's Score: " + new_score.ToString ();
		}
		else
		{
			scoreText.text = "Team Score: " + new_score.ToString ();
		}
	}

	public bool IsCompeting()
	{
		return competing;
	}

	public bool Stunned()
	{
		return stunned;
	}

	public int GetDamage()
	{
		return damage;
	}

	public void AddBoost()
	{
		speed = 2.5f;
		damage = 20;
		Invoke ("RemoveBoost", 20.0f);
	}

	public void Stun()
	{
		stunned = true;
		Invoke ("RemoveBoost", 10.0f);
	}

	void OnChangeSpeed(float new_speed)
	{
		speed = new_speed;
	}

	void OnStun(bool s)
	{
		stunned = s;
	}

	void OnChangeDamage(int new_damage)
	{
		damage = new_damage;
	}

	public void RemoveBoost()
	{
		//return to default speed and damage settings
		speed = 1.2f;
		stunned = false;
		damage = 10;
	}

	void OnTriggerEnter(Collider other)
	{
		//Debug.Log ("Player entered trigger");
		//first find normal of point where player entered
		var collisionPoint = other.ClosestPointOnBounds (transform.position);
		Ray ray = new Ray (transform.position, collisionPoint - transform.position);
		RaycastHit hit;
		other.Raycast (ray, out hit, 10.0f);
		var normal = hit.normal;
		normal = Vector3.ProjectOnPlane (normal, new Vector3 (0, 1, 0));
		normal.Normalize ();
		//move player away from collision point along normal
		transform.position = collisionPoint + 1.2f*GetComponent<CapsuleCollider>().radius*normal;
	}

	bool OutOfBounds()
	{
		if (transform.position.x > maxPosition.x || transform.position.z > maxPosition.z
			|| transform.position.x < minPosition.x || transform.position.z < minPosition.z)
			return true;
		else
			return false;
	}

	void AllowRepeatFire()
	{
		firstTriggerPress = true;
	}
}
