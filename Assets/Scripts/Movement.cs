// Base taken from Mix and Jam: https://www.youtube.com/watch?v=STyY26a_dPY

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// Added for class 
public enum PlayerState 
{
    IDLE,
    RUNNING,
    CLIMBING,
    SLIDING,
    FALLING,
    JUMPING,
    WALLJUMPING,
    DASHING,
    NONMOVEABLE,
}

// Other states to consider: ON_WALL, JUMPING, FALLING, DASHING, WALL_JUMPING
// You may also need to move code into the states I've already made

// An alternative idea would be to make a few larger states like GROUNDED, AIRBORN, ON_WALL
// Then each state has a larger chunk of code that deals with each area

// How you choose to implement the states is up to you
// The goal is to make the code easier to understand and easier to expand on

public class Movement : MonoBehaviour
{
    // Use this to check the state
    public PlayerState currentState = PlayerState.IDLE;

    // Custom collision script
    private Collision coll;
    
    [HideInInspector]
    public Rigidbody2D rb;
    private AnimationScript anim;

    [Space] // Adds some space in the inspector
    [Header("Stats")] // Adds a header in the inspector 
    public float speed = 10;
    public float jumpForce = 50;
    public float slideSpeed = 5;
    public float wallJumpLerp = 10;
    public float dashSpeed = 20;

    [Space]
    [Header("Booleans")]

    public bool groundTouch;
    private bool hasDashed;

    // Input Variables
    private float xInput;
    private float yInput;
    private float xRaw;
    private float yRaw;
    private Vector2 inputDirection;

    private void SetInputVariables()
    {
        xInput = Input.GetAxis("Horizontal");
        yInput = Input.GetAxis("Vertical");
        xRaw = Input.GetAxisRaw("Horizontal");
        yRaw = Input.GetAxisRaw("Vertical");
        inputDirection = new Vector2(xInput, yInput);
    }

    // Start is called before the first frame update
    void Start()
    {
        coll = GetComponent<Collision>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<AnimationScript>();

        SetInputVariables();
    }

    private void LateUpdate()
    {

        #region checkGroundTouch
        // When you land on the ground
        if (coll.onGround && !groundTouch)
        {
            GroundTouch();
            groundTouch = true;
        }

        // When you have left the ground
        if (!coll.onGround && groundTouch)
        {
            groundTouch = false;
        }
        #endregion

    }

    // Update is called once per frame
    void Update()
    {
        // Set input data for easy access
        SetInputVariables();

        // Reset Gravity
        rb.gravityScale = 3;

        #region state logic
        //actions placed in order for override

        //running
        if ((xInput > 0.01f || xInput < -0.01f) && groundTouch)
        {
            currentState = PlayerState.RUNNING;
        }

        //wall climbing
        if (rb.velocity.y < 0.1f) //ONLY CLIMB ON FALL
        {
            if (Input.GetButton("Fire2") && coll.onWall)
            {
                currentState = PlayerState.CLIMBING;
            }
            //wall sliding
            else if (coll.onWall && !groundTouch)
            {
                currentState = PlayerState.SLIDING;
            }
        }

        //jumping
        if (Input.GetButtonDown("Jump"))
        {
            if (groundTouch)
            {
                currentState = PlayerState.JUMPING;
                groundTouch = false;
            }
            else if (coll.onWall)
            {
                currentState = PlayerState.WALLJUMPING;
            }
        }
        //dashing
        if (Input.GetButtonDown("Fire1") && !hasDashed)
        {
            currentState = PlayerState.DASHING;
        }


        #endregion

        // Use the statemachine
        StateMachine(currentState);


    }

    private void StateMachine(PlayerState state)
    {
        // This is where the code for each state goes
        switch (state)
        {
            case PlayerState.IDLE:
                //does nothing
            break;

            case PlayerState.RUNNING:

                // Use input direction to move and change the animation
                Walk(inputDirection);
                anim.SetHorizontalMovement(xInput, yInput, rb.velocity.y);
            
                // Condition: No horizontal input, go to IDLE state
                if(xInput <= 0.01f || xInput >= 0.01f)
                {
                    currentState = PlayerState.IDLE;
                }

            break;
            
            case PlayerState.CLIMBING:
            
                // Stop gravity
                rb.gravityScale = 0;

                // Limit horizontal movement
                if(xInput > .2f || xInput < -.2f)
                {
                    rb.velocity = new Vector2(rb.velocity.x, 0);
                }
            
                // Vertical Movement, slower when climbing
                float speedModifier = yInput > 0 ? .5f : 1;
                rb.velocity = new Vector2(rb.velocity.x, yInput * (speed * speedModifier));

                if (!coll.onWall)
                {
                    currentState = PlayerState.FALLING;
                }

                // Leave Condition:
                if (!coll.onWall || !Input.GetButton("Fire2"))
                {
                    // Change state to default
                    currentState = PlayerState.IDLE;

                    // Reset Gravity
                    rb.gravityScale = 3;
                }
                break;

            case PlayerState.SLIDING:
                WallSlide();

                // Limit horizontal movement
                if (xInput > .2f || xInput < -.2f)
                {
                    rb.velocity = new Vector2(rb.velocity.x, 0);
                }

                if (!coll.onWall) {
                    currentState = PlayerState.FALLING;
                }

                // Leave Condition:
                if (!coll.onWall || !Input.GetButton("Fire2"))
                {
                    // Change state to default
                    currentState = PlayerState.IDLE;

                    // Reset Gravity
                    rb.gravityScale = 3;
                }

                break;

            case PlayerState.FALLING:
                Walk(inputDirection);
                break;

            case PlayerState.JUMPING:
                anim.SetTrigger("jump");
                Jump(Vector2.up, false);
                currentState = PlayerState.FALLING;

                break;

            case PlayerState.WALLJUMPING:
                anim.SetTrigger("jump");
                WallJump();
                break;

            case PlayerState.DASHING:
                // As long as there is some directional input
                if (xRaw != 0 || yRaw != 0)
                    // Dash using raw input values
                    Dash(xRaw, yRaw);

                break;
            case PlayerState.NONMOVEABLE:
                //this is intentionally empty
                break;
        }
    }

    void GroundTouch()
    {
        // Reset dash
        hasDashed = false;
    }


    private void Dash(float x, float y)
    {
        // Graphics effects
        Camera.main.transform.DOComplete();
        Camera.main.transform.DOShakePosition(.2f, .5f, 14, 90, false, true);
        FindObjectOfType<RippleEffect>().Emit(Camera.main.WorldToViewportPoint(transform.position));

        // Put dash on cooldown
        hasDashed = true;

        anim.SetTrigger("dash");


        rb.velocity = Vector2.zero;
        Vector2 dir = new Vector2(x, y);

        rb.velocity += dir.normalized * dashSpeed;
        StartCoroutine(DashWait());

        if (!groundTouch) {     
            currentState = PlayerState.FALLING;
        }
    }

    IEnumerator DashWait()
    {   
        // Graphics effect for trail
        FindObjectOfType<GhostTrail>().ShowGhost();
        
        // Resets dash right away if on ground 
        StartCoroutine(GroundDash());

        // Changes drag over time
        DOVirtual.Float(14, 0, .8f, SetRigidbodyDrag);

        // Stop gravity
        rb.gravityScale = 0;

        // Disable better jumping script
        GetComponent<BetterJumping>().enabled = false;

        // Wait for dash to end
        yield return new WaitForSeconds(.3f);

        // Reset gravity
        rb.gravityScale = 3;

        // Turn better jumping back on
        GetComponent<BetterJumping>().enabled = true;
    }

    IEnumerator GroundDash()
    {   
        // Resets dash right away
        yield return new WaitForSeconds(.15f);
        if (coll.onGround)
            hasDashed = false;
    }

    private void WallJump()
    {

        // Disable movement while wall jumping
        StopCoroutine(DisableMovement(0));
        StartCoroutine(DisableMovement(.1f));

        // Set direction based on which wall
        Vector2 wallDir = coll.onRightWall ? Vector2.left : Vector2.right;

        currentState = PlayerState.NONMOVEABLE;

        // Jump using the direction
        Jump((Vector2.up / 1.5f + wallDir / 1.5f), true);
    }

    private void WallSlide()
    {   
        // If the player is holding towards the wall...
        bool pushingWall = false;
        if((rb.velocity.x > 0 && coll.onRightWall) || (rb.velocity.x < 0 && coll.onLeftWall))
        {
            pushingWall = true;
        }
        float push = pushingWall ? 0 : rb.velocity.x;

        // Move down
        rb.velocity = new Vector2(push, -slideSpeed);
    }

    private void Walk(Vector2 dir)
    {           
        rb.velocity = new Vector2(dir.x * speed, rb.velocity.y);
    }

    private void Steer(Vector2 dir) {
        rb.velocity = Vector2.Lerp(rb.velocity, (new Vector2(dir.x * speed, rb.velocity.y)), wallJumpLerp * Time.deltaTime);
    }

    private void Jump(Vector2 dir, bool wall)
    {
        rb.velocity = new Vector2(rb.velocity.x, 0);
        rb.velocity += dir * jumpForce;
    }

    IEnumerator DisableMovement(float time)
    {
        yield return new WaitForSeconds(time);

        if (!groundTouch)
        {
            currentState = PlayerState.FALLING;
        }
    }

    void SetRigidbodyDrag(float x)
    {
        rb.drag = x;
    }

}
