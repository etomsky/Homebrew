﻿using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HomebrewFlags))]
public class Player : Responsive {
    private const float DASH_THRESHOLD = 0.5f;
    private const float MOVE_THRESHOLD = 0.1f;
    private const float DASH_MULTIPLIER = 2f;
    private const float SELECTOR_TIME_SCALE = 0.5f;
    private const float IFRAME_HZ = 10;

    public float maxSpeed = 4f;
    public float friction = 0.2f;
    public float jump = 4f;
    public GameObject bottle;
    public float attackX;                  //Attack x position
    public float attackY;                  //Attack y position
    private bool aimingMode;
    public float bottlespeed;
    public float launchRadius;
    public GameObject reticle;

    public GameObject selectorOverlay;

    public Elements[] elements = new Elements[2] {
        Elements.FIRE, Elements.NONE
    };

    public Text[] elementText;
    public Text combinedText;
    public Sprite[] playerHealthSprites;

    public Image uiHealthImage;

    private float timeSinceJump;
    private float timeToDash;
    private bool dashing;
    private bool autodashing;
    private SpriteRenderer sprite;

    private int activeElement;

    private bool[] ownedElements;
    private int[] elementMap;
    private GameObject[] quadrants;
    private Text[] quadrantText;

    // Use this for initialization
    protected override void Awake() {
        base.Awake();

        if (Me == null) {
            Me = this;
        } else {
            throw new System.Exception("Can't have two players at once");
        }

        HomebrewGame.AddMob(gameObject);
        
        SetHealth();

        elementMap = new int[4] {
            (int)Elements.NONE, (int)Elements.FIRE, (int)Elements.WATER, (int)Elements.EARTH
        };

        ownedElements = new bool[(int)Elements.SIZE] {
            true, false, true, false, true, false, true,
            // combination elements
            false, false, false
        };

        quadrants = new GameObject[4] {
            selectorOverlay.transform.Find("Ring 0").gameObject,
            selectorOverlay.transform.Find("Ring 1").gameObject,
            selectorOverlay.transform.Find("Ring 2").gameObject,
            selectorOverlay.transform.Find("Ring 3").gameObject
        };

        quadrantText = new Text[4] {
            selectorOverlay.transform.Find("Text 0").GetComponent<Text>(),
            selectorOverlay.transform.Find("Text 1").GetComponent<Text>(),
            selectorOverlay.transform.Find("Text 2").GetComponent<Text>(),
            selectorOverlay.transform.Find("Text 3").GetComponent<Text>()
        };

        timeSinceJump = 0f;
        timeToDash = 0f;
        dashing = false;
        autodashing = false;

        activeElement = 0;

        reticle.SetActive(false);

        GetComponent<HomebrewFlags>().Set(Elements.PLAYER);

        SetElementText();
    }

    // This used to be FixedUpdate but it was causing inputs to be skipped whenever the FixedUpdate was
    // fired at a different time than regular Update because screw you too Unity
    protected override void Update() {
        base.Update();

        // because bad things would probably happen if you were allowed to move in the pause menu,
        // where Time.timeScale = 0
        if (HomebrewGame.Me.menu.IsPaused) {
            return;
        }
        // invincibility

        if (IFrames > 0f) {
            IFrames = Mathf.Max(IFrames - Time.deltaTime, 0f);

            Renderer body = GetComponent<Renderer>();

            if (IFrames > 0f) {
                IFrameTime = Mathf.Max(IFrameTime - Time.deltaTime);
                if (IFrameTime <= 0f) {
                    body.enabled = (IFrames > 0f ? !body.enabled : true);
                    IFrameTime = 1 / IFRAME_HZ;
                }
            } else {
                body.enabled = true;
            }
        }

        float horizontal = Input.GetAxis("Horizontal");

        /*
         * Running
         */
        
        if (Mathf.Abs(horizontal) > MOVE_THRESHOLD) {
            if (Input.GetButtonDown("Run")) {
                if (timeToDash > 0f) {
                    autodashing = true;
                } else {
                    timeToDash = DASH_THRESHOLD;
                }
            }

            if (Input.GetButton("Run")) {
                dashing = true;
                timeToDash = DASH_THRESHOLD;
            } else {
                dashing = false;
            }
        }

        timeToDash = Mathf.Max(0f, timeToDash - Time.deltaTime);

        /*
         * Move around
         */

        if (Mathf.Abs(horizontal) < MOVE_THRESHOLD) {
            dashing = false;
            autodashing = false;
        }

        if (Mathf.Abs(horizontal) > 0.25f) {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontal);
            transform.localScale = scale;
        }
        
        float f = speedFactor;

        if (dashing || autodashing) {
            f = DASH_MULTIPLIER;
        }

        Rigidbody2D plugandplay = GetComponent<Rigidbody2D>();
        
        if (GameSettings.MovementStyle == MovementStyles.SMOOTH) {
            // SMOOTH MOVEMENT
            plugandplay.AddForce(new Vector2(horizontal * f, 0f), ForceMode2D.Impulse);
        } else {
            // SNAPPY MOVEMENT
            Vector2 currentVelocity = GetComponent<Rigidbody2D>().velocity;
            GetComponent<Rigidbody2D>().velocity = new Vector2(Mathf.Round(horizontal) * f * maxSpeed, currentVelocity.y);
        }
        
        Vector2 velocity = plugandplay.velocity;
        velocity.x = Mathf.Clamp(velocity.x, -maxSpeed * f, maxSpeed * f);
        
        bool grounded = Physics2D.OverlapPoint(new Vector2(transform.position.x, transform.position.y - 0.75f), HomebrewFlags.EnvironmentalCollisionMask());

        if (grounded) {
            if (Input.GetButtonDown("Jump")) {
                timeSinceJump = 0f;
            }
            
            velocity.x = Mathf.Lerp(velocity.x, 0f, friction);
            
        }

        timeSinceJump = timeSinceJump + Time.deltaTime;

        plugandplay.velocity = velocity;

        // after the velocity has been set, process jumping. this re-reads and resets the velocity so if
        // you do it before it's set above it'll get overwritten.

        if (GameSettings.MovementStyle == MovementStyles.SMOOTH) {
            // SMOOTH MOVEMENT
            if (timeSinceJump < 0.15f && Input.GetButton("Jump")) {
                plugandplay.gravityScale = 0f;
                Jump();
            } else {
                plugandplay.gravityScale = 1f;
            }
        } else {
            // SNAPPY MOVEMENT
            plugandplay.gravityScale = 1f;
            if (grounded && Input.GetButtonDown("Jump")) {
                // this magic number would produce *about-ish* the same jump height as holding your finger
                // down on the button in Smooth mode
                Jump(10f);
                // fact: mathematics tells me the value should be more like 7.3f but that value isn't working
                // and i don't want to touch it now
            }
        }
        /*
         * Throw, but only if the overlay(s) aren't shown
         */

        if (!selectorOverlay.activeInHierarchy) {
            Vector3 mousePos2D = Input.mousePosition;
            mousePos2D.z = -Camera.main.transform.position.z;
            Vector3 mousePos3d = Camera.main.ScreenToWorldPoint(mousePos2D);
            Vector3 mouseDelta = mousePos3d - transform.position;

            if (aimingMode && Input.GetButtonUp("Potion Chuck")) {
                aimingMode = false;

                Vector3 pvelocity = mouseDelta;
                pvelocity.Normalize();

                GameObject bottleClone = Instantiate(bottle);

                bottleClone.transform.position = transform.position;
                bottleClone.GetComponent<Rigidbody2D>().isKinematic = false;
                bottleClone.GetComponent<Rigidbody2D>().velocity = pvelocity * bottlespeed;

                bottleClone.transform.position = reticle.transform.position;

                PersistentInteraction.ApplyToBottle(bottleClone, elements[0], elements[1], gameObject);

                reticle.SetActive(false);

                GameAudio.Me.PlayBottle();


            }

            if (Input.GetButtonDown("Potion Chuck")) {
                //the player has pressed the mouse button down while over the slingshot 
                aimingMode = true;

                reticle.SetActive(true);
            }

            if (aimingMode) {
                //limit mouse delta to the radius of the slingshot spherecollider
                float maxmagnitude = launchRadius;

                Vector3 absMouseDelta = mouseDelta;
                absMouseDelta.Normalize();
                absMouseDelta = absMouseDelta * maxmagnitude;

                if (mouseDelta.magnitude > maxmagnitude) {
                    mouseDelta.Normalize();
                    mouseDelta *= maxmagnitude;
                }

                reticle.transform.position = transform.position + absMouseDelta;
            }

            GetComponent<Animator>().SetFloat("Speed", Mathf.Abs(horizontal));
        }


        /*
         * Cycle selected element
         */

        if (Input.GetButtonDown("Cycle")) {
            activeElement = (++activeElement) % elements.Length;
            SetElementText();
        }

        /*
         * Potions menu: bottom left: (0, 0); top right: (W, H)
         */

        Vector2 position = new Vector2((Input.mousePosition.x / Screen.width) - 0.5f, (Input.mousePosition.y / Screen.height) - 0.5f);

        if (selectorOverlay.activeInHierarchy) {
            foreach (GameObject what in quadrants) {
                what.SetActive(false);
            }

            if (position.magnitude > 0.1f) {
                quadrants[Quadrant(position)].SetActive(true);
            }

            if (Input.GetButtonDown("Select")) {
                selectorOverlay.SetActive(false);
                Time.timeScale = 1f;

                if (position.magnitude > 0.1f) {
                    elements[activeElement] = (Elements)elementMap[Quadrant(position)];
                    SetElementText();
                }
            }
        } else {
            if (Input.GetButtonDown("Potion Menu")) {
                for (int i = 0; i < 4; i++) {
                    Text text = quadrantText[i];
                    text.text = ownedElements[elementMap[i]] ? PersistentInteraction.Me.elementNames[elementMap[i]] : "None";
                }

                selectorOverlay.SetActive(true);
                Time.timeScale = SELECTOR_TIME_SCALE;
            }
        }
    }

    private void Jump() {
        Jump(jump);
    }

    private void Jump(float vspeed) {
        Vector2 velocity = GetComponent<Rigidbody2D>().velocity;
        velocity = velocity + Vector2.up * vspeed;
        GetComponent<Rigidbody2D>().velocity = velocity;
        GameAudio.Me.PlayJump();
    }

    public static Player Me {
        get; private set;
    }

    public float IFrames {
        get; set;
    }

    private float IFrameTime {
        get; set;
    }

    public bool Invincible {
        get {
            return IFrames > 0f;
        }
    }

    public void AutoIFrames() {
        // this is completely arbitrary
        IFrames = 1f;
        IFrameTime = 1 / IFRAME_HZ;
    }

    private int Quadrant(Vector2 position) {
        if (position.x > 0f) {
            // upper right
            if (position.y > 0f) {
                return 0;
                // lower right
            } else {
                return 1;
            }
        } else {
            // upper left
            if (position.y > 0f) {
                return 3;
                // lower left
            } else {
                return 2;
            }
        }
    }

    private void SetElementText() {
        for (int i = 0; i < elementText.Length; i++) {
            Sprite sprite = PersistentInteraction.Me.elementSprites[(int)elements[i]];
            if (sprite == null) {
                elementText[i].transform.Find("Sprite").GetComponent<Image>().enabled = false;
                elementText[i].text = "Element " + (i + 1) + ": None";
            } else {
                elementText[i].transform.Find("Sprite").GetComponent<Image>().enabled = true;
                elementText[i].transform.Find("Sprite").GetComponent<Image>().sprite = sprite;
                elementText[i].text = "Element " + (i + 1) + ":";
            }
            // this is an arbitrary color to indicate the active element, DO SOMETHING LESS AMBIGUOUS LATER
            elementText[i].color = (activeElement == i) ? Color.cyan : Color.white;
        }

        combinedText.text = "}   " + PersistentInteraction.Me.Data(elements[0], elements[1]).Name;
    }

    public virtual void Damage(int amount) {
        if (IFrames <= 0f) {
            health = health - amount;
            AutoIFrames();
            if (health > 0) {
                OnDamage(amount);
            } else {
                // die
            }
        }
    }

    public override void OnDamage(int amount) {
        base.OnDamage(amount);
        GameAudio.Me.PlayHitPlayer();
    }

    protected override void SetHealth() {
        uiHealthImage.sprite = playerHealthSprites[health];
    }
}