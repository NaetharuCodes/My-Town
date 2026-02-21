using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Agent : MonoBehaviour
{
    [Header("Identity")]
    public string agentName;

    [Header("Personality")]
    public Personality personality = new Personality();
    public bool isBeingChased = false;

    [Header("Police Officer")]
    public Agent chasingTarget;
    public int officerArrestFine;

    // Internal police officer state
    private float chasePathTimer;
    private float patrolWaitTimer;
    private float normalMoveSpeed;

    [Header("Needs")]
    [Range(0f, 100f)]
    public float hunger = 0f;
    public float hungerRate = 0.5f;
    public float hungerThreshold = 60f;

    [Header("Social")]
    [Range(0f, 100f)]
    public float loneliness = 0f;
    public float lonelinessRate = 0.2f;
    public float lonelinessThreshold = 60f;
    public float visitDuration = 10f;
    public float visitLonelinessRestored = 70f;

    [Header("Finances")]
    public int bankBalance;
    public int dailyIncome = 70; // Placeholder — replaced by wage once agent has a job

    [Header("State")]
    public AgentState currentState = AgentState.Idle;
    public Vector3Int homeTile;
    public bool hasHome = false;

    [Header("Employment")]
    public bool hasJob = false;
    public CommercialBuilding employer;
    public Shift assignedShift;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Eating & Cooking")]
    public float eatDuration = 3f;
    public float cookingDuration = 5f;
    public float cookingHungerRestored = 80f;
    public int pantryRestockThreshold = 2; // Go grocery shopping if pantry drops below this

    // Internal
    private List<Vector3Int> currentPath;
    private Vector3Int foodTile;
    private Vector3Int groceryTile;
    private Vector3Int parkTile;
    private float eatTimer;
    private float cookingTimer;
    private float visitTimer;
    private int pathIndex;
    private Vector3 targetWorldPos;
    private bool isMoving = false;
    private float thinkTimer = 0f;
    private const float ThinkInterval = 5f; // seconds between Idle priority evaluations

    // Cached routes for key destinations (home, work) so A* only runs once per route.
    // Keyed by destination; stores the origin so we only use the cache on matching starts.
    // Future: expose which destinations count as "key" via a UI toggle.
    private readonly Dictionary<Vector3Int, (Vector3Int from, List<Vector3Int> path)> routeCache
        = new Dictionary<Vector3Int, (Vector3Int, List<Vector3Int>)>();

    private Inventory carriedItems = new Inventory();
    private DwellingUnit homeDwelling;

    private AgentManager agentManager;
    private BuildingManager buildingManager;
    private Pathfinder pathfinder;
    private Tilemap buildingsTilemap;
    private TimeManager timeManager;

    // --- Inventory API (used by stores) ---
    public void AddToInventory(ItemType type, int count = 1) => carriedItems.Add(type, count);
    public int CarriedCount(ItemType type) => carriedItems.Get(type);

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
        if (!hasJob)
        {
            bankBalance += dailyIncome;
            Debug.Log($"{agentName} received ${dailyIncome} income. Balance: ${bankBalance}");
        }
    }

    void Update()
    {
        hunger    = Mathf.Min(hunger    + hungerRate    * Time.deltaTime, 100f);
        loneliness = Mathf.Min(loneliness + lonelinessRate * Time.deltaTime, 100f);

        if (isMoving)
        {
            MoveAlongPath();
            return;
        }

        switch (currentState)
        {
            case AgentState.Idle:           HandleIdle();            break;
            case AgentState.SeekingHome:    HandleSeekingHome();     break;
            case AgentState.SeekingWork:    HandleSeekingWork();     break;
            case AgentState.SeekingFood:    HandleSeekingFood();     break;
            case AgentState.SeekingGroceries: HandleSeekingGroceries(); break;
            case AgentState.Eating:         HandleEating();          break;
            case AgentState.Cooking:        HandleCooking();         break;
            case AgentState.Working:        HandleWorking();         break;
            case AgentState.AtPark:         HandleAtPark();          break;
            case AgentState.SeekingPark:    HandleSeekingPark();     break;
            case AgentState.Fleeing:        HandleFleeing();         break;
            case AgentState.Arrested:       /* coroutine handles release */ break;
            case AgentState.Patrolling:     HandlePatrolling();      break;
            case AgentState.Chasing:        HandleChasing();         break;
        }
    }

    void HandleIdle()
    {
        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        thinkTimer = ThinkInterval;

        // Priority 1: work — shift takes precedence over everything.
        if (hasJob && assignedShift != null && timeManager != null)
        {
            if (assignedShift.IsActiveAt(timeManager.CurrentHour))
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell != employer.gridPosition)
                {
                    StartPathTo(employer.gridPosition);
                    currentState = AgentState.WalkingToWork;
                }
                else
                {
                    employer.WorkerCheckIn(this);
                    currentState = AgentState.Working;
                }
                return;
            }
        }

        // Priority 2: hunger — prefer cooking at home if pantry is stocked.
        if (hunger >= hungerThreshold)
        {
            if (homeDwelling != null && homeDwelling.pantry.Has(ItemType.Groceries))
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell == homeTile)
                {
                    // Already home — start cooking.
                    cookingTimer = cookingDuration;
                    currentState = AgentState.Cooking;
                }
                else
                {
                    // Walk home to cook rather than going out.
                    StartPathTo(homeTile);
                    currentState = AgentState.WalkingHome;
                }
                return;
            }

            // No pantry stock — only go out if somewhere is open.
            Vector3Int cell = buildingsTilemap.WorldToCell(transform.position);
            bool eateryOpen = buildingManager.FindNearest<BurgerStore>(cell, b => b.IsOpen()).HasValue;
            if (eateryOpen)
            {
                currentState = AgentState.SeekingFood;
                return;
            }
        }

        // Priority 3: go home if not already there.
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

        // Priority 4: find a home.
        if (!hasHome)
        {
            currentState = AgentState.SeekingHome;
            return;
        }

        // Priority 5: restock pantry if running low.
        if (homeDwelling != null && homeDwelling.pantry.Get(ItemType.Groceries) < pantryRestockThreshold)
        {
            Vector3Int cell = buildingsTilemap.WorldToCell(transform.position);
            bool supermarketOpen = buildingManager.FindNearest<Supermarket>(cell, b => b.IsOpen()).HasValue;
            if (supermarketOpen)
            {
                currentState = AgentState.SeekingGroceries;
                return;
            }
        }

        // Priority 6: social — visit a park if lonely.
        if (loneliness >= lonelinessThreshold)
        {
            Vector3Int cell = buildingsTilemap.WorldToCell(transform.position);
            if (buildingManager.FindNearest<Park>(cell).HasValue)
            {
                currentState = AgentState.SeekingPark;
                return;
            }
        }

        // Priority 7: find a job.
        if (!hasJob)
        {
            currentState = AgentState.SeekingWork;
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
                    currentState = AgentState.Idle;
            }
            else
                currentState = AgentState.Idle;
        }
        else
            currentState = AgentState.Idle;
    }

    void HandleSeekingWork()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? jobPos = buildingManager.FindNearest<CommercialBuilding>(
            currentCell, b => b.HasShiftVacancy());

        if (jobPos.HasValue)
        {
            CommercialBuilding building = (CommercialBuilding)buildingManager.GetBuildingAt(jobPos.Value);
            Shift shift = building.TryHire(this);
            if (shift != null)
            {
                employer = building;
                assignedShift = shift;
                hasJob = true;
                Debug.Log($"{agentName} got a job at {building.buildingName}. " +
                          $"Shift: {shift.startHour}:00 – {(shift.startHour + shift.durationHours) % 24}:00");
            }
        }

        currentState = AgentState.Idle;
    }

    void HandleSeekingFood()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? store = buildingManager.FindNearest<BurgerStore>(currentCell, b => b.IsOpen());

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
                currentState = AgentState.Idle;
        }
        else
            currentState = AgentState.Idle;
    }

    void HandleSeekingGroceries()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? market = buildingManager.FindNearest<Supermarket>(currentCell, b => b.IsOpen());

        if (market.HasValue)
        {
            var path = pathfinder.FindPath(currentCell, market.Value);
            if (path != null)
            {
                groceryTile = market.Value;
                StartFollowingPath(path);
                currentState = AgentState.WalkingToSupermarket;
            }
            else
                currentState = AgentState.Idle;
        }
        else
            currentState = AgentState.Idle;
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
                currentState = AgentState.Idle;
        }
    }

    void HandleCooking()
    {
        cookingTimer -= Time.deltaTime;
        if (cookingTimer <= 0f)
        {
            if (homeDwelling != null && homeDwelling.pantry.Remove(ItemType.Groceries))
            {
                Feed(cookingHungerRestored);
                Debug.Log($"{agentName} cooked and ate at home. Pantry has {homeDwelling.pantry.Get(ItemType.Groceries)} groceries left.");
            }
            currentState = AgentState.Idle;
        }
    }

    void HandleSeekingPark()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? park = buildingManager.FindNearest<Park>(currentCell);

        if (park.HasValue)
        {
            var path = pathfinder.FindPath(currentCell, park.Value);
            if (path != null)
            {
                parkTile = park.Value;
                StartFollowingPath(path);
                currentState = AgentState.WalkingToPark;
            }
            else
                currentState = AgentState.Idle;
        }
        else
            currentState = AgentState.Idle;
    }

    void HandleAtPark()
    {
        visitTimer -= Time.deltaTime;
        if (visitTimer <= 0f)
        {
            loneliness = Mathf.Max(loneliness - visitLonelinessRestored, 0f);
            Debug.Log($"{agentName} enjoyed the park. Loneliness: {loneliness:0}");
            if (hasHome)
            {
                StartPathTo(homeTile);
                currentState = AgentState.WalkingHome;
            }
            else
                currentState = AgentState.Idle;
        }
    }

    void HandleWorking()
    {
        if (timeManager != null && !assignedShift.IsActiveAt(timeManager.CurrentHour))
        {
            employer.WorkerCheckOut(this);
            currentState = AgentState.Idle;
        }
    }

    void HandleFleeing()
    {
        // Not moving means the path finished without OnPathComplete handling it,
        // or StartPathTo failed to find a route. Try again or confirm safe arrival.
        if (!isMoving)
        {
            if (hasHome)
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell == homeTile)
                {
                    isBeingChased = false;
                    Debug.Log($"{agentName} made it home and evaded the police!");
                    currentState = AgentState.Idle;
                }
                else
                {
                    StartPathTo(homeTile); // retry
                }
            }
        }
    }

    void HandlePatrolling()
    {
        // End shift — check out and go idle like any other worker.
        if (timeManager != null && assignedShift != null &&
            !assignedShift.IsActiveAt(timeManager.CurrentHour))
        {
            employer.WorkerCheckOut(this);
            currentState = AgentState.Idle;
            return;
        }

        // While stationary, count down to next patrol leg.
        if (!isMoving)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                Vector3Int dest = GetRandomPatrolPoint();
                StartPathTo(dest);
                patrolWaitTimer = Random.Range(8f, 20f);
            }
        }
    }

    void HandleChasing()
    {
        // Shift ended while chasing — give up and check out.
        if (timeManager != null && assignedShift != null &&
            !assignedShift.IsActiveAt(timeManager.CurrentHour))
        {
            if (chasingTarget != null) chasingTarget.isBeingChased = false;
            EndChase();
            employer.WorkerCheckOut(this);
            currentState = AgentState.Idle;
            return;
        }

        // Target gone (destroyed, or already arrested by someone else).
        if (chasingTarget == null || chasingTarget.currentState == AgentState.Arrested)
        {
            EndChase();
            return;
        }

        // Criminal made it home — give up.
        if (chasingTarget.hasHome && chasingTarget.currentState != AgentState.Fleeing)
        {
            Vector3Int criminalCell = buildingsTilemap.WorldToCell(chasingTarget.transform.position);
            if (criminalCell == chasingTarget.homeTile)
            {
                Debug.Log($"{agentName} lost {chasingTarget.agentName} — they made it home.");
                chasingTarget.isBeingChased = false;
                EndChase();
                return;
            }
        }

        // Arrest range check.
        if (Vector3.Distance(transform.position, chasingTarget.transform.position) <= 0.7f)
        {
            chasingTarget.Arrest(officerArrestFine);
            Debug.Log($"{agentName} arrested {chasingTarget.agentName}!");
            chasingTarget = null;
            EndChase();
            return;
        }

        // Periodically recalculate path toward the moving criminal.
        chasePathTimer -= Time.deltaTime;
        if (chasePathTimer <= 0f)
        {
            chasePathTimer = 1.5f;
            Vector3Int target = buildingsTilemap.WorldToCell(chasingTarget.transform.position);
            StartPathTo(target);
        }
    }

    void EndChase()
    {
        moveSpeed = normalMoveSpeed;
        chasingTarget = null;
        currentState = AgentState.Patrolling;
        patrolWaitTimer = 3f; // brief pause before resuming patrol
    }

    Vector3Int GetRandomPatrolPoint()
    {
        const int radius = 6;
        Vector3Int center = employer != null ? employer.gridPosition : buildingsTilemap.WorldToCell(transform.position);

        for (int attempt = 0; attempt < 15; attempt++)
        {
            int dx = Random.Range(-radius, radius + 1);
            int dy = Random.Range(-radius, radius + 1);
            Vector3Int candidate = center + new Vector3Int(dx, dy, 0);
            if (pathfinder.IsRoadAt(candidate))
                return candidate;
        }

        return center; // fallback — stand at the station if no road found
    }

    // True for destinations we travel to regularly — routes are worth caching.
    bool IsKeyDestination(Vector3Int dest)
    {
        if (hasHome && dest == homeTile) return true;
        if (hasJob && dest == employer.gridPosition) return true;
        return false;
    }

    void StartPathTo(Vector3Int target)
    {
        Vector3Int from = buildingsTilemap.WorldToCell(transform.position);

        // Use cached route if we're starting from the same tile it was computed from.
        if (IsKeyDestination(target) &&
            routeCache.TryGetValue(target, out var cached) &&
            cached.from == from)
        {
            StartFollowingPath(new List<Vector3Int>(cached.path));
            return;
        }

        var path = pathfinder.FindPath(from, target);
        if (path != null)
        {
            if (IsKeyDestination(target))
                routeCache[target] = (from, new List<Vector3Int>(path));
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

            // Before stepping onto the next tile, verify it's still a road.
            // Skip the check for the destination tile itself — buildings are always reachable.
            if (pathIndex < currentPath.Count - 1 && !pathfinder.IsRoadAt(currentPath[pathIndex]))
            {
                HandleBlockedRoute();
                return;
            }

            SetNextTarget();
        }
    }

    void HandleBlockedRoute()
    {
        Vector3Int dest = currentPath[currentPath.Count - 1];

        // Discard the cached route — it's stale due to a map change.
        // Don't cache the reroute: we're mid-journey at a random tile, not at the
        // canonical start (home/work). The cache will be rebuilt correctly next time
        // the agent starts this trip from its usual origin.
        routeCache.Remove(dest);

        Vector3Int here = buildingsTilemap.WorldToCell(transform.position);
        var rerouted = pathfinder.FindPath(here, dest);
        if (rerouted != null)
        {
            currentPath = rerouted;
            pathIndex = 0;
            SetNextTarget();
        }
        else
        {
            // No route exists from here — give up and re-evaluate needs.
            isMoving = false;
            thinkTimer = 0f;
            currentState = AgentState.Idle;
            Debug.Log($"{agentName}: route to destination is blocked, returning to Idle.");
        }
    }

    void OnPathComplete()
    {
        switch (currentState)
        {
            case AgentState.WalkingToWork:
                employer.WorkerCheckIn(this);
                if (employer is PoliceStation)
                {
                    patrolWaitTimer = 2f;
                    currentState = AgentState.Patrolling;
                    Debug.Log($"{agentName} reporting for duty at {employer.buildingName}.");
                }
                else
                {
                    currentState = AgentState.Working;
                    Debug.Log($"{agentName} arrived at work.");
                }
                break;

            case AgentState.WalkingToFood:
                Building foodBuilding = buildingManager.GetBuildingAt(foodTile);
                if (foodBuilding != null && foodBuilding.Interact(this))
                {
                    eatTimer = eatDuration;
                    currentState = AgentState.Eating;
                }
                else
                    currentState = AgentState.Idle;
                break;

            case AgentState.WalkingToSupermarket:
                Building market = buildingManager.GetBuildingAt(groceryTile);
                if (market != null)
                    market.Interact(this); // Groceries land in carriedItems; success or not, head home
                StartPathTo(homeTile);
                currentState = AgentState.WalkingHome;
                break;

            case AgentState.WalkingHome:
                // Deposit any carried groceries into the home pantry on arrival.
                if (homeDwelling != null && carriedItems.Has(ItemType.Groceries))
                {
                    int count = carriedItems.Get(ItemType.Groceries);
                    homeDwelling.pantry.Add(ItemType.Groceries, count);
                    carriedItems.Remove(ItemType.Groceries, count);
                    Debug.Log($"{agentName} put {count} groceries in the pantry. " +
                              $"Pantry now has {homeDwelling.pantry.Get(ItemType.Groceries)}.");
                }
                currentState = AgentState.Idle;
                break;

            case AgentState.SeekingHome:
                currentState = AgentState.Idle;
                break;

            case AgentState.WalkingToPark:
                Building parkBuilding = buildingManager.GetBuildingAt(parkTile);
                if (parkBuilding != null && parkBuilding.Interact(this))
                {
                    visitTimer = visitDuration;
                    currentState = AgentState.AtPark;
                }
                else
                    currentState = AgentState.Idle;
                break;

            case AgentState.Fleeing:
                // Deposit any carried groceries on safe arrival home.
                if (homeDwelling != null && carriedItems.Has(ItemType.Groceries))
                {
                    int count = carriedItems.Get(ItemType.Groceries);
                    homeDwelling.pantry.Add(ItemType.Groceries, count);
                    carriedItems.Remove(ItemType.Groceries, count);
                }
                isBeingChased = false;
                Debug.Log($"{agentName} made it home safely and evaded the police!");
                currentState = AgentState.Idle;
                break;
        }
    }

    // --- Public API ---

    public void AssignHome(Vector3Int tile, DwellingUnit dwelling)
    {
        homeTile = tile;
        homeDwelling = dwelling;
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

    public void ReceiveWage(int amount)
    {
        bankBalance += amount;
        Debug.Log($"{agentName} received wage ${amount}. Balance: ${bankBalance}");
    }

    public bool TrySpend(int price)
    {
        if (bankBalance >= price)
        {
            bankBalance -= price;
            return true;
        }
        return false;
    }

    // Called by PoliceStation when assigning this officer to chase a criminal.
    public void AssignChase(Agent criminal, int fine)
    {
        chasingTarget      = criminal;
        officerArrestFine  = fine;
        normalMoveSpeed    = moveSpeed;
        moveSpeed          = 4f;   // officers sprint during a chase
        isMoving           = false; // interrupt current patrol movement
        chasePathTimer     = 0f;   // recalculate path immediately
        currentState       = AgentState.Chasing;
        criminal.NotifyPoliceChasing();
        Debug.Log($"{agentName} is pursuing {criminal.agentName}!");
    }

    // Called when a police officer is assigned to chase this agent.
    public void NotifyPoliceChasing()
    {
        if (currentState == AgentState.Arrested) return;
        isBeingChased = true;
        isMoving = false; // interrupt current movement
        currentState = AgentState.Fleeing;
        if (hasHome)
            StartPathTo(homeTile);
        Debug.Log($"{agentName} is fleeing from the police!");
    }

    // Called by an officer agent when they catch this agent. Fine deducted immediately.
    public void Arrest(int fine)
    {
        isBeingChased = false;
        bankBalance -= fine;
        Debug.Log($"{agentName} was arrested! Lost ${fine} as a fine. Balance: ${bankBalance}");
        currentState = AgentState.Arrested;
        StartCoroutine(ReleaseFromArrest());
    }

    private System.Collections.IEnumerator ReleaseFromArrest()
    {
        yield return new WaitForSeconds(5f);
        Debug.Log($"{agentName} was released from custody.");
        currentState = AgentState.Idle;
        thinkTimer = 0f;
    }
}

public enum AgentState
{
    Idle,
    SeekingHome,
    SeekingWork,
    SeekingFood,
    SeekingGroceries,
    SeekingPark,
    WalkingToWork,
    WalkingToFood,
    WalkingToSupermarket,
    WalkingToPark,
    Eating,
    Cooking,
    Working,
    AtPark,
    WalkingHome,
    Fleeing,
    Arrested,
    Patrolling,
    Chasing
}
