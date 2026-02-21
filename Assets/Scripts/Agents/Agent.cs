using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Agent : MonoBehaviour
{
    [Header("Identity")]
    public string agentName;

    [Header("Needs")]
    [Range(0f, 100f)]
    public float hunger = 0f;
    public float hungerRate = 0.5f;
    public float hungerThreshold = 60f;

    [Header("Finances")]
    public int bankBalance;
    public int dailyIncome = 70; // Placeholder until the jobs system is implemented

    [Header("State")]
    public AgentState currentState = AgentState.Idle;
    public Vector3Int homeTile;
    public bool hasHome = false;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Eating")]
    public float eatDuration = 3f;

    // Internal
    private List<Vector3Int> currentPath;
    private Vector3Int foodTile;
    private float eatTimer;
    private int pathIndex;
    private Vector3 targetWorldPos;
    private bool isMoving = false;

    private AgentManager agentManager;
    private BuildingManager buildingManager;
    private Pathfinder pathfinder;
    private Tilemap buildingsTilemap;
    private TimeManager timeManager;

    public void Initialise(AgentManager manager, Pathfinder pf, Tilemap buildings, BuildingManager bm)
    {
        agentManager = manager;
        pathfinder = pf;
        buildingsTilemap = buildings;
        buildingManager = bm;
        bankBalance = Random.Range(200, 500);
    }

    void Start()
    {
        timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
            timeManager.OnNewDay += ReceiveDailyIncome;
    }

    void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnNewDay -= ReceiveDailyIncome;
    }

    void ReceiveDailyIncome(int day)
    {
        bankBalance += dailyIncome;
        Debug.Log($"{agentName} received ${dailyIncome} income. Balance: ${bankBalance}");
    }

    void Update()
    {
        hunger = Mathf.Min(hunger + hungerRate * Time.deltaTime, 100f);

        if (isMoving)
        {
            MoveAlongPath();
            return;
        }

        switch (currentState)
        {
            case AgentState.Idle:
                HandleIdle();
                break;
            case AgentState.SeekingHome:
                HandleSeekingHome();
                break;
            case AgentState.SeekingFood:
                HandleSeekingFood();
                break;
            case AgentState.Eating:
                HandleEating();
                break;
        }
    }

    void HandleIdle()
    {
        if (hunger >= hungerThreshold)
        {
            currentState = AgentState.SeekingFood;
            return;
        }

        if (hasHome)
        {
            Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
            if (currentCell != homeTile)
            {
                StartPathTo(homeTile);
                currentState = AgentState.WalkingHome;
                return;
            }
        }

        if (!hasHome)
        {
            currentState = AgentState.SeekingHome;
            return;
        }
    }

    void HandleSeekingHome()
    {
        Vector3Int? home = buildingManager.FindAvailableHome();
        if (home.HasValue)
        {
            Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
            var path = pathfinder.FindPath(currentCell, home.Value);

            if (path != null)
            {
                Building building = buildingManager.GetBuildingAt(home.Value);
                if (building is ResidentialBuilding residential && residential.Interact(this))
                {
                    StartFollowingPath(path);
                    currentState = AgentState.WalkingHome;
                }
                else
                {
                    currentState = AgentState.Idle;
                }
            }
            else
            {
                currentState = AgentState.Idle;
            }
        }
        else
        {
            currentState = AgentState.Idle;
        }
    }
    void HandleSeekingFood()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? store = buildingManager.FindNearest<BurgerStore>(currentCell);

        if (store.HasValue)
        {
            var path = pathfinder.FindPath(currentCell, store.Value);
            if (path != null)
            {
                foodTile = store.Value;
                StartFollowingPath(path);
                currentState = AgentState.WalkingToFood;
            }
            else
            {
                currentState = AgentState.Idle;
            }
        }
        else
        {
            currentState = AgentState.Idle;
        }
    }

    void HandleEating()
    {
        eatTimer -= Time.deltaTime;
        if (eatTimer <= 0f)
        {
            if (hasHome)
            {
                StartPathTo(homeTile);
                currentState = AgentState.WalkingHome;
            }
            else
            {
                currentState = AgentState.Idle;
            }
        }
    }
    void StartPathTo(Vector3Int target)
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        var path = pathfinder.FindPath(currentCell, target);
        if (path != null)
        {
            StartFollowingPath(path);
        }
    }

    void StartFollowingPath(List<Vector3Int> path)
    {
        currentPath = path;
        pathIndex = 0;
        SetNextTarget();
    }

    void SetNextTarget()
    {
        if (pathIndex < currentPath.Count)
        {
            targetWorldPos = buildingsTilemap.CellToWorld(currentPath[pathIndex])
                             + new Vector3(0.5f, 0.5f, 0f);
            isMoving = true;
        }
        else
        {
            isMoving = false;
            OnPathComplete();
        }
    }

    void MoveAlongPath()
    {
        transform.position = Vector3.MoveTowards(
            transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPos) < 0.01f)
        {
            pathIndex++;
            SetNextTarget();
        }
    }

    void OnPathComplete()
    {
        switch (currentState)
        {
            case AgentState.WalkingToFood:
                Building foodBuilding = buildingManager.GetBuildingAt(foodTile);
                if (foodBuilding != null && foodBuilding.Interact(this))
                {
                    eatTimer = eatDuration;
                    currentState = AgentState.Eating;
                }
                else
                {
                    currentState = AgentState.Idle;
                }
                break;
            case AgentState.WalkingHome:
                currentState = AgentState.Idle;
                break;
            case AgentState.SeekingHome:
                currentState = AgentState.Idle;
                break;
        }
    }

    public void AssignHome(Vector3Int tile)
    {
        homeTile = tile;
        hasHome = true;
    }

    public void Feed(float amount)
    {
        hunger = Mathf.Max(hunger - amount, 0f);
    }

    public void ChargeRent(int amount)
    {
        bankBalance -= amount;
        if (bankBalance < 0)
            Debug.Log($"{agentName} can't afford rent! Balance: ${bankBalance}");
        else
            Debug.Log($"{agentName} paid ${amount} rent. Balance: ${bankBalance}");
    }

    public bool TrySpend(int price)
    {
        if (bankBalance >= price)
        {
            bankBalance -= price;
            return true;
        }
        else
        {
            return false;
        }
    }
}

public enum AgentState
{
    Idle,
    SeekingHome,
    SeekingFood,
    WalkingToFood,
    Eating,
    WalkingHome
}