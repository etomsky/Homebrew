﻿using UnityEngine;

public class EnemyFollow : Enemy {
    private const float FOLLOW_DISTANCE = 5f;

    public float speed = 1f;
    private Transform target;

    private bool right;

    // Use this for initialization
    protected override void Awake() {
        base.Awake();
        target = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();

        right = true;
    }

    // Update is called once per frame
    protected override void Update() {
        base.Update();
        
        if (Vector2.Distance(target.position, transform.position) <= FOLLOW_DISTANCE) {
            if ((target.position.x < transform.position.x && right) ^ (target.position.x > transform.position.x && !right)) {
                Turn();
            }
            transform.position = Vector2.MoveTowards(transform.position, new Vector2(target.position.x, transform.position.y), speed * Time.deltaTime);
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            Vector2 velocity = body.velocity;
            velocity.x = 0f;
            body.velocity = velocity;
        }
    }

    protected override void OnCollisionEnter2D(Collision2D other) {
        base.OnCollisionEnter2D(other);
    }

    protected override void Turn() {
        base.Turn();
        right = !right;
    }

    // only un-comment if you need to override
//    public override void Kill(GameObject who) {
//        base.Kill(who);
//    }

    public override void Interact(int potionFlags) {
        base.Interact(potionFlags);
    }
}
