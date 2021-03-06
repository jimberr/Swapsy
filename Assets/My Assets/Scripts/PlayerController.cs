using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : ObjectController {

    // Variables that our players/NPCs need
    public float speed = 10f;

    Animator anim;

    public Vector3 cameraDistance = new Vector3(0, 16, -4);
    public Vector2 cameraMaxTilt = new Vector2(-90, 20);

    public Vector3 modelRotationOffset = Vector3.zero;

    public ObjectController[] objectsInArea;
    public GameObject closestObj;

    private GameObject toBecome;

    public override void Start() {
        base.Start();

        anim = GetComponent<Animator>();

        if (gameObject.tag == "Player") {
            becomePlayer();
            objectsInArea = new ObjectController[] { };
        }

        actionable = true;
    }

	// Returns what the default message should be if one isnt already set (overrides object setting)
	public override string defaultNearbyMessage() {
		return "(x) Talk\n(c) Swap body";
	}

    // Helper function that returns objects within a certain range (that aren't the player or part of the world)
    ObjectController[] getObjectsInRange() {
        ObjectController[] objects = Camera.main.GetComponent<GameManager>().getObjects();

        if (objects == null || objects.Length <= 0) {
            return objects;
        }

        List<ObjectController> sorted = new List<ObjectController>();

        float dot = -2f;
        for (int i = 0; i < objects.Length; i++) {
            ObjectController obj = objects[i];

            if (obj != null && obj.transform.position != transform.position && Vector3.Distance(obj.gameObject.GetComponent<Collider>().ClosestPointOnBounds(transform.position), GetComponent<Collider>().ClosestPointOnBounds(transform.position)) <= Mathf.Max(interactionRange, obj.interactionRange)) {
                if (gameObject.tag == "Player") {
                    obj.displayInteractions();
                    obj.setNearPlayer(true);

                    Vector3 localPoint = Camera.main.transform.InverseTransformPoint(obj.transform.position).normalized;
                    float test = Vector3.Dot(localPoint, Vector3.forward);
                    if (test > dot && obj.actionable) { // we only care about the closest actionable object
                        dot = test;
                        if (closestObj) {
                            closestObj.GetComponent<ObjectController>().colorText(Color.white);
                        }
                        closestObj = obj.gameObject;
                        obj.colorText(Color.red);
                    }

                }
                sorted.Add(obj);
            }
        }

        // Sort through old nearby objects, disable any that are no longer in range
        foreach (ObjectController obj in objectsInArea) {
            if (!sorted.Contains(obj)) {
                obj.setNearPlayer(false);
                obj.hideText();
            }
        }

        return sorted.ToArray();
    }

    public void clearObjectsInArea() {
        foreach (ObjectController obj in objectsInArea) {
            obj.setNearPlayer(false);
            obj.hideText();
        }

        objectsInArea = new ObjectController[] { };
    }

    // Update
    public virtual void FixedUpdate() {
        if (gameObject.tag == "Player") {
            movePlayer();
            scanAndInteract();
        }
    }

    // At the end of the draw cycle we swap, that way no two objects can be the 'player' in the same draw cycle
    public void LateUpdate() {
        if (toBecome != null) {
            swapWithObject(toBecome);
        }
    }

    public void updateObjectsInRange() {
        objectsInArea = getObjectsInRange();
    }

    // Update animation parameters
    public virtual void updateAnimation(float speed, bool running) {
        if (anim != null) {
            anim.SetFloat("Speed", speed);
            anim.SetBool("Running", running);
        }
    }

    // Moves the player based on 'Horizontal'and 'Vertical' Axes (Input)
    public virtual void movePlayer() {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        bool sprint = Input.GetButton("Sprint");

        // Apply movement
        Vector3 movement = new Vector3(moveX, 0.0f, moveZ);
        movement = Vector3.ClampMagnitude(movement, 1f);
        movement = Camera.main.transform.rotation * movement; // Rotate our movement to match the camera so that we always go forward from where we are looking
        movement.y = 0.0f;

        movement *= speed;
        if (sprint) {
            movement *= 10;
        }

        if (movement.magnitude > 0) {
            GetComponent<Rigidbody>().rotation = Quaternion.LookRotation(movement) * Quaternion.Euler(modelRotationOffset); // Turn to face the direction we are moving
        }

        GetComponent<Rigidbody>().velocity = movement;

        // Animate
        updateAnimation(Mathf.Max(Mathf.Abs(moveX), Mathf.Abs(moveZ)), sprint);
    }

    // Scans for objects I can interact with nearby, and handle interactions
    public void scanAndInteract() {
        updateObjectsInRange();

        for (int i = 0; i < objectsInArea.Length; i++) {
            GameObject obj = objectsInArea[i].gameObject;

			if (obj == closestObj) {
				obj.GetComponent<ObjectController>().handleInteractions(gameObject);
			}
        }
    }

	// Handles being interacted with by the player
	public override void handleInteractions(GameObject cause) {
		if (gameObject.tag == "PlayerChoice") {
			if (Input.GetButtonUp("Talk")) {
				speak();
			} else if (Input.GetButtonUp("Swap")) {
				cause.GetComponent<PlayerController>().swapAfterFrame(gameObject);
				return;
			}
		}
	}

	public void swapAfterFrame(GameObject obj) {
		toBecome = obj;
	}

    // Handle taking over a new object
    public virtual void swapWithObject(GameObject obj) {
        gameObject.tag = "PlayerChoice"; // this object (the previous player) is now an interactable choice
        updateAnimation(0.0f, false); // we clear the animation settings so it will go back to Idle
        clearObjectsInArea();
        handleLocking();
        obj.GetComponent<PlayerController>().becomePlayer(); // the other object is now the player, we control him
        pointCameraAfterSwap(obj);
        toBecome = null;
    }

    public void pointCameraAfterSwap(GameObject obj) {
        Camera.main.GetComponent<CameraController>().point(obj.GetComponent<PlayerController>().lookAfterSwap(gameObject)); // Rotate camera so new player is looking at old player
    }

    public virtual Vector3 lookAfterSwap(GameObject previous) {
        return transform.position + transform.rotation * Vector3.forward;
    }

    // When you've been switched to
    public virtual void becomePlayer() {
        if (gameObject.tag != "Player") {
            updateObjectsInRange();
        }
        gameObject.tag = "Player";
        handleLocking();
        hideText();
        Camera.main.GetComponent<CameraController>().setTarget(gameObject.transform); // Have camera follow you
        Camera.main.GetComponent<CameraController>().setConstraints(cameraMaxTilt); // Limit the camera to a certain angle 
		Camera.main.GetComponent<GameManager>().setCurrentPlayer(gameObject);
    }
}
