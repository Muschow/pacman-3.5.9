using Godot;
using System;
using System.Collections.Generic;

public class GhostScript : CharacterScript
{
    private Movement moveScr = new Movement();
    private Vector2[] nodeList;
    private Vector2[] adjList;
    private int mazeOrigin;
    private List<Vector2> paths;
    private KinematicBody2D pacman;
    private TileMap nodeTilemap;
    private int mazeheight;
    private int Gspeed = 100; //maybe just have a constructor with speed = 100 instead of this Gspeed crap
    private int pathCounter = 0;
    private bool recalculate = false;
    Vector2 movementV;
    private Timer chaseTimer;
    private Timer patrolTimer;
    private Timer vulnerableTimer;
    private int randNodeIndex;

    protected enum states
    {
        patrol,
        chase,
        vulnerable
    }
    private states ghostState = states.patrol; //initialise ghost state to patrol. Timer randomly switches states from patrol to chase and vice versa

    protected override void MoveAnimManager(Vector2 masVector)
    {
        AnimatedSprite ghostEyes = GetNode<AnimatedSprite>("GhostEyes"); //not sure whether to put it in here for readabillity or in each ready so theres less calls

        masVector = masVector.Normalized().Round();


        GD.Print(masVector);
        if (masVector == Vector2.Up)
        {
            ghostEyes.Play("up");
        }
        else if (masVector == Vector2.Down)
        {
            ghostEyes.Play("down");
        }
        else if (masVector == Vector2.Right)
        {
            ghostEyes.Play("right");
        }
        else if (masVector == Vector2.Left)
        {
            ghostEyes.Play("left");
        }
    }
    //As GhostScript is a base class, it will not be in the scene tree so ready and process are not needed
    // Called when the node enters the scene tree for the first time.

    public override void _Ready()
    {
        GD.Print("ghostscript ready");

        mazeTm = GetParent().GetNode<TileMap>("MazeTilemap");
        nodeTilemap = GetParent().GetNode<TileMap>("NodeTilemap");
        pacman = GetNode<KinematicBody2D>("/root/Game/Pacman");

        chaseTimer = GetNode<Timer>("ChaseTimer");
        patrolTimer = GetNode<Timer>("PatrolTimer");
        vulnerableTimer = GetNode<Timer>("VulnerableTimer");

        nodeList = (Vector2[])mazeTm.Get("nodeList");
        adjList = (Vector2[])mazeTm.Get("adjList");
        mazeOrigin = (int)mazeTm.Get("mazeOriginY");
        mazeheight = (int)mazeTm.Get("height");


        Position = new Vector2(1, mazeOrigin + 1) * 32 + new Vector2(16, 16); //spawn ghost on top left of current maze

        randNodeIndex = (int)GD.RandRange(0, nodeList.Length);
        paths = moveScr.Dijkstras(mazeTm.WorldToMap(Position), mazeTm.WorldToMap(pacman.Position), nodeList, adjList);

        EnterState(ghostState); //initialise first ghostState (patrol);

        GD.Print("ready paths count", paths.Count);
        GD.Print("world to map pos ", mazeTm.WorldToMap(Position));
        GD.Print("map to world wtm pos ", mazeTm.MapToWorld(mazeTm.WorldToMap(Position)));
        GD.Print("curr pos ", Position);
        GD.Print("ready pacman pos", pacman.Position);

    }

    private states EnterState(states ghostState)
    {
        if (ghostState == states.patrol)
        {
            patrolTimer.Start((float)GD.RandRange(2, 15));
        }
        else if (ghostState == states.chase)
        {
            chaseTimer.Start((float)GD.RandRange(10, 60));
        }
        else if (ghostState == states.vulnerable)
        {
            vulnerableTimer.Start(15);
        }

        return ghostState;
    }

    private void OnResetChasePathTimeout() //recalculates pathfinding when timer timeouts
    {
        recalculate = true; //every x seconds, set recalculate to true
    }

    private void OnChaseTimerTimeout()
    {
        ghostState = EnterState(states.patrol); //has to be in here as this is called once and not every frame
    }

    private void OnPatrolTimerTimeout()
    {
        ghostState = EnterState(states.chase);

    }

    private void OnVulnerableTimerTimeout()
    {
        patrolTimer.Paused = false;
        ghostState = EnterState(states.patrol);

    }

    private bool IsOnNode(Vector2 pos)
    {

        if (nodeTilemap.GetCellv(pos) == Globals.NODE)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private Vector2 FindClosestNodeTo(Vector2 targetVector)
    {
        //the node must have the same x or y as targetPos
        int shortestInt = Globals.INFINITY;
        Vector2 shortestNode = Vector2.Inf;

        foreach (Vector2 node in nodeList)
        {
            if ((node.y == targetVector.y || node.x == targetVector.x) && (node != targetVector))
            {
                int currShortestInt = Math.Abs(moveScr.ConvertVecToInt(targetVector - node));
                if (currShortestInt < shortestInt)
                {
                    shortestInt = currShortestInt;
                    shortestNode = node;
                }

            }
        }

        return shortestNode;
    }
    private void FindNewPath(Vector2 sourcePos, Vector2 targetPos)
    {
        pathCounter = 0;

        //if targetpos is not in nodeList (essentially, if pacman is between nodes...)
        if (!IsOnNode(targetPos))
        {
            GD.Print("THIS IS GETTING CALLED!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            GD.Print("targetpos ", targetPos);
            GD.Print("mazeorigin+height-1 ", mazeOrigin + mazeheight - 1);
            //search for the closest vector in nodelist to targetPos that is not targetpos, make that = targetPos.
            targetPos = FindClosestNodeTo(targetPos);
        }
        paths = moveScr.Dijkstras(sourcePos, targetPos, nodeList, adjList); //pathfind to the new targetPos
    }

    private void MoveToAndValidatePos(float delta)
    {
        if (Position.IsEqualApprox(mazeTm.MapToWorld(paths[pathCounter]) + new Vector2(16, 16))) //must use IsEqualApprox with vectors due to floating point precision errors instead of ==
        {
            pathCounter++; //if ghost position == node position then increment
        }
        else
        {
            movementV = Position.MoveToward(mazeTm.MapToWorld(paths[pathCounter]) + new Vector2(16, 16), delta * Gspeed); //if not, move toward node position
            Position = movementV;
            MoveAnimManager(paths[pathCounter] - mazeTm.WorldToMap(Position));
            // GD.Print("Position ", Position);
        }
    }

    private void GhostChase(float delta)
    {


        if (mazeTm.WorldToMap(pacman.Position).y < (mazeOrigin + mazeheight - 1))
        {
            if (IsOnNode(mazeTm.WorldToMap(Position)) && recalculate) //every x seconds, if pacman and ghost is on a node, it recalulates shortest path.
            {
                recalculate = false;
                FindNewPath(mazeTm.WorldToMap(Position), mazeTm.WorldToMap(pacman.Position));
            }

            if (pathCounter < paths.Count)
            {
                MoveToAndValidatePos(delta);
                //GD.Print(pathCounter);
            }
            else if (pathCounter >= paths.Count) //if its reached the end of its path, calculate new path
            {
                FindNewPath(mazeTm.WorldToMap(Position), mazeTm.WorldToMap(pacman.Position));
            }
        }

    }

    private void GhostPatrol(float delta)
    {

        GD.Print(patrolTimer.WaitTime);

        if (pathCounter < paths.Count)
        {
            MoveToAndValidatePos(delta);
            //GD.Print(pathCounter);
        }
        else if (pathCounter >= paths.Count) //if its reached the end of its path, calculate new path
        {
            randNodeIndex = (int)GD.RandRange(0, nodeList.Length);
            FindNewPath(mazeTm.WorldToMap(Position), nodeList[randNodeIndex]);
        }
    }

    private void GhostVulnerable(float delta)
    {

        GhostPatrol(delta);
        animatedSprite.Play("vulnerable");
        //if ghost collides with pacman, kill ghost, give pacman like 100 points and increase multiplier

        //on leaving scatter, play the normal one again. On ready, play the normal one to intitialise.
    }

    private void ProcessStates(float delta)
    {
        if (ghostState == states.patrol)
        {
            GD.Print("PATROL STATE-----------------------------------------");
            GhostPatrol(delta);
        }
        else if (ghostState == states.chase)
        {
            GD.Print("CHASE STATE-----------------------------------------");
            GhostChase(delta);
        }
        else if (ghostState == states.vulnerable)
        {
            GD.Print("VULNERABLE STATE-----------------------------------------");
            chaseTimer.Stop();          //stop chase and patrol timer just in case
            patrolTimer.Stop();
            patrolTimer.Paused = true; //pauses patrol timer as scatter uses patrol mode for pathfinding

            GhostVulnerable(delta);
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(float delta)
    {
        PlayAndPauseAnim(movementV);
        ProcessStates(delta);
    }
}

