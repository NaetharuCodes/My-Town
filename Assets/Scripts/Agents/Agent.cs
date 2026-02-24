using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Agent : MonoBehaviour
{
    [Header("Identity")]
    public string agentName;
    public LifeStage lifeStage = LifeStage.Adult;
    public int ageInYears = 25;
    public Family family;
    public FamilyRole familyRole = FamilyRole.Head;

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

    // Internal firefighter state
    private Building burningBuilding;
    private float extinguishTimer;
    private const float ExtinguishDuration = 8f;

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

    [Header("Health")]
    public List<ActiveCondition> conditions = new List<ActiveCondition>();

    [Header("State")]
    public AgentState currentState = AgentState.Idle;
    public Vector3Int homeTile;
    public bool hasHome = false;

    [Header("Employment")]
    public bool hasJob = false;
    public CommercialBuilding employer;
    public Shift assignedShift;

    [Header("School")]
    public bool isEnrolled = false;
    public SchoolBuilding enrolledSchool;
    private Vector3Int schoolTile;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Eating & Cooking")]
    public float eatDuration = 3f;
    public float cookingDuration = 5f;
    public float cookingHungerRestored = 80f;
    public int pantryRestockThreshold = 2; // Go grocery shopping if pantry drops below this

    [Header("Homeless Behaviour")]
    [Tooltip("Real seconds an agent will wait for housing before leaving town.")]
    public float homelessLeaveTime = 120f;

    // Health / medical internals
    private Vector3Int medicalTile;
    private float treatmentTimer;
    private MedicalBuilding currentMedicalBuilding;
    private float diseaseCheckTimer;
    private const float DiseaseCheckInterval = 5f;

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
    private float homelessTimer = 0f;       // total time spent without a home
    private float homelessRetryTimer = 0f;  // countdown to next home-search attempt
    private const float HomelessRetryInterval = 3f; // re-check every 3 s when homeless

    // Cached routes for key destinations (home, work) so A* only runs once per route.
    // Keyed by destination; stores the origin so we only use the cache on matching starts.
    private readonly Dictionary<Vector3Int, (Vector3Int from, List<Vector3Int> path)> routeCache
        = new Dictionary<Vector3Int, (Vector3Int, List<Vector3Int>)>();

    private Inventory carriedItems = new Inventory();
    private DwellingUnit homeDwelling;
    public DwellingUnit HomeDwelling => homeDwelling;

    private AgentManager agentManager;
    private BuildingManager buildingManager;
    private Pathfinder pathfinder;
    private Tilemap buildingsTilemap;
    private TimeManager timeManager;

    // --- Life stage capability helpers ---
    public bool CanSeekWork()        => lifeStage == LifeStage.Adult;
    public bool CanShopAlone()       => lifeStage >= LifeStage.Teen;
    public bool CanCookAlone()       => lifeStage >= LifeStage.YoungChild;
    public bool CanVisitPark()       => lifeStage >= LifeStage.YoungChild;
    public bool NeedsParentFeed()    => lifeStage == LifeStage.Baby || lifeStage == LifeStage.Toddler;
    public bool CanAttendSchool()    => lifeStage >= LifeStage.YoungChild && lifeStage <= LifeStage.Teen;
    public bool CanAttendPreschool() => lifeStage == LifeStage.Toddler;

    // --- Health helpers ---
    public bool IsSick        => conditions.Exists(c => c.symptomatic);
    public bool NeedsHospital => conditions.Exists(c => c.symptomatic &&
                                     c.data.severity >= ConditionSeverity.Serious);
    public bool NeedsDoctor   => conditions.Exists(c => c.symptomatic &&
                                     c.data.severity == ConditionSeverity.Mild);

    // --- Inventory API (used by stores) ---
    public void AddToInventory(ItemType type, int count = 1) => carriedItems.Add(type, count);
    public int CarriedCount(ItemType type) => carriedItems.Get(type);

    public void Initialise(AgentManager manager, Pathfinder pf, Tilemap buildings, BuildingManager bm,
                           LifeStage stage = LifeStage.Adult, int age = 25)
    {
        agentManager = manager;
        pathfinder = pf;
        buildingsTilemap = buildings;
        buildingManager = bm;
        lifeStage = stage;
        ageInYears = age;
        bankBalance = Random.Range(200, 500);
        ApplyLifeStageRates();
    }

    void ApplyLifeStageRates()
    {
        switch (lifeStage)
        {
            case LifeStage.Baby:
                hungerRate = 0.15f;
                lonelinessRate = 0f;
                break;
            case LifeStage.Toddler:
                hungerRate = 0.25f;
                lonelinessRate = 0.05f;
                break;
            // YoungChild through VenerableElder use the default rates set in the Inspector
        }
    }

    void Start()
    {
        timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
            timeManager.OnNewDay += ProgressConditions;
    }

    void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnNewDay -= ProgressConditions;
    }

    void Update()
    {
        hunger    = Mathf.Min(hunger    + hungerRate    * Time.deltaTime, 100f);
        loneliness = Mathf.Min(loneliness + lonelinessRate * Time.deltaTime, 100f);

        if (!hasHome)
        {
            homelessTimer += Time.deltaTime;
            if (homelessTimer >= homelessLeaveTime)
            {
                LeaveTown();
                return;
            }
        }

        // Periodically spread contagious conditions to nearby agents.
        diseaseCheckTimer -= Time.deltaTime;
        if (diseaseCheckTimer <= 0f)
        {
            diseaseCheckTimer = DiseaseCheckInterval;
            CheckDiseaseSpread();
        }

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
            case AgentState.Fleeing:           HandleFleeing();           break;
            case AgentState.Arrested:          /* coroutine handles release */ break;
            case AgentState.Patrolling:        HandlePatrolling();        break;
            case AgentState.Chasing:           HandleChasing();           break;
            case AgentState.RespondingToFire:  HandleRespondingToFire();  break;
            case AgentState.Extinguishing:     HandleExtinguishing();     break;
            case AgentState.SeekingSchool:          HandleSeekingSchool();          break;
            case AgentState.AtSchool:               HandleAtSchool();               break;
            case AgentState.SeekingHomelessShelter: HandleSeekingHomelessShelter(); break;
            case AgentState.SeekingDoctor:          HandleSeekingDoctor();          break;
            case AgentState.AtDoctor:               HandleAtMedical();              break;
            case AgentState.SeekingHospital:        HandleSeekingHospital();        break;
            case AgentState.AtHospital:             HandleAtMedical();              break;
        }
    }

    void HandleIdle()
    {
        // When homeless, re-check for housing on its own fast timer, independent of thinkTimer.
        // This ensures a newly built house is noticed within a few seconds rather than up to
        // ThinkInterval seconds later.
        if (!hasHome)
        {
            homelessRetryTimer -= Time.deltaTime;
            if (homelessRetryTimer <= 0f)
            {
                homelessRetryTimer = HomelessRetryInterval;
                currentState = AgentState.SeekingHome;
                return;
            }
        }

        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        thinkTimer = ThinkInterval;

        // Babies stay home entirely — no preschool for infants.
        if (lifeStage == LifeStage.Baby)
        {
            if (hasHome)
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell != homeTile && StartPathTo(homeTile))
                    currentState = AgentState.WalkingHome;
            }
            return;
        }

        // School/Preschool attendance — highest priority for children, mirrors work priority for adults.
        if (CanAttendPreschool() || CanAttendSchool())
        {
            if (isEnrolled && enrolledSchool != null && timeManager != null
                && enrolledSchool.IsInSession(timeManager.CurrentHour))
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell != enrolledSchool.gridPosition)
                {
                    if (StartPathTo(enrolledSchool.gridPosition))
                    {
                        schoolTile = enrolledSchool.gridPosition;
                        currentState = AgentState.WalkingToSchool;
                        return;
                    }
                    // School is unreachable — fall through to other priorities this tick.
                }
                else
                {
                    enrolledSchool.StudentArrive(this);
                    currentState = AgentState.AtSchool;
                    return;
                }
            }
        }

        // Toddlers outside preschool hours stay home — still need parent feeding.
        if (NeedsParentFeed())
        {
            if (hasHome)
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell != homeTile && StartPathTo(homeTile))
                    currentState = AgentState.WalkingHome;
            }
            return;
        }

        // Priority 0: medical emergency — serious/critical illness overrides everything.
        if (NeedsHospital)
        {
            currentState = AgentState.SeekingHospital;
            return;
        }

        // Priority 1: work — shift takes precedence over everything. Adults only.
        if (CanSeekWork() && hasJob && employer != null && assignedShift != null && timeManager != null)
        {
            if (assignedShift.IsActiveAt(timeManager.CurrentHour))
            {
                Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
                if (currentCell != employer.gridPosition)
                {
                    if (StartPathTo(employer.gridPosition))
                        currentState = AgentState.WalkingToWork;
                }
                else
                {
                    employer.WorkerCheckIn(this);
                    if (employer is PoliceStation || employer is FireStation)
                    {
                        patrolWaitTimer = 2f;
                        currentState = AgentState.Patrolling;
                    }
                    else
                        currentState = AgentState.Working;
                }
                return;
            }
        }

        // Priority 2: hunger — prefer cooking at home if pantry is stocked and old enough to cook.
        if (hunger >= hungerThreshold)
        {
            if (CanCookAlone() && homeDwelling != null && homeDwelling.pantry.Has(ItemType.Groceries))
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
                    if (StartPathTo(homeTile))
                        currentState = AgentState.WalkingHome;
                }
                return;
            }

            // No pantry stock (or can't cook) — only go out if somewhere is open.
            Vector3Int cell = buildingsTilemap.WorldToCell(transform.position);
            bool eateryOpen = buildingManager.FindNearest<BurgerStore>(cell, b => b.IsOpen()).HasValue;
            if (eateryOpen)
            {
                currentState = AgentState.SeekingFood;
                return;
            }
        }

        // Priority 2.5: mild illness — see a doctor when hunger is satisfied.
        if (NeedsDoctor)
        {
            currentState = AgentState.SeekingDoctor;
            return;
        }

        // Priority 3: go home if not already there.
        if (hasHome)
        {
            Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
            if (currentCell != homeTile)
            {
                if (StartPathTo(homeTile))
                {
                    currentState = AgentState.WalkingHome;
                    return;
                }
                // Home is unreachable from here — release it so the agent can seek a new one.
                ClearHome();
            }
        }

        // Priority 4: find a home (handled above via homelessRetryTimer; skip here).
        // If still homeless after that check, seek shelter or a park to rest.
        if (!hasHome)
        {
            currentState = AgentState.SeekingHomelessShelter;
            return;
        }

        // Priority 5: restock pantry if running low. Shopping requires being old enough.
        if (CanShopAlone() && homeDwelling != null && homeDwelling.pantry.Get(ItemType.Groceries) < pantryRestockThreshold)
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
        if (CanVisitPark() && loneliness >= lonelinessThreshold)
        {
            Vector3Int cell = buildingsTilemap.WorldToCell(transform.position);
            if (buildingManager.FindNearest<Park>(cell).HasValue)
            {
                currentState = AgentState.SeekingPark;
                return;
            }
        }

        // Priority 7: find a job. Adults only.
        if (CanSeekWork() && !hasJob)
        {
            currentState = AgentState.SeekingWork;
            return;
        }

        // Priority 8: seek school/preschool enrollment if not yet enrolled.
        if ((CanAttendPreschool() || CanAttendSchool()) && !isEnrolled)
        {
            currentState = AgentState.SeekingSchool;
            return;
        }
    }

    void HandleSeekingHome()
    {
        // If part of a family, check whether another member already has a home — join their dwelling.
        if (family != null)
        {
            foreach (Agent member in family.members)
            {
                if (member == this || !member.hasHome) continue;

                // Join their dwelling unit directly.
                DwellingUnit sharedUnit = member.HomeDwelling;
                if (sharedUnit != null && !sharedUnit.DwellingOccupancy.Contains(this))
                {
                    AssignHome(member.homeTile, sharedUnit);
                    if (StartPathTo(homeTile))
                    {
                        sharedUnit.DwellingOccupancy.Add(this);
                        currentState = AgentState.WalkingHome;
                        return;
                    }
                    // Can't reach sibling's home — clear and fall through to find own
                    hasHome = false;
                    homeTile = Vector3Int.zero;
                    homeDwelling = null;
                }
            }
        }

        // No family or no housed family member — find the best available unit.
        int neededBedrooms = family != null ? family.members.Count : 1;
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);

        foreach (Vector3Int home in buildingManager.FindAvailableHomes())
        {
            Building building = buildingManager.GetBuildingAt(home);
            if (building is not ResidentialBuilding residential) continue;

            DwellingUnit unit = residential.FindBestVacantUnit(neededBedrooms);
            if (unit == null) continue;

            var path = pathfinder.FindPath(currentCell, home);
            if (path == null) continue;

            // Assign this agent (family members will join via the sibling check above).
            unit.DwellingOccupancy.Add(this);
            AssignHome(home, unit);
            StartFollowingPath(path);
            currentState = AgentState.WalkingHome;
            return;
        }

        // No reachable home found this cycle — try again later.
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
                EventLog.Log($"{agentName} got a job at {building.buildingName}.");
                Debug.Log($"{agentName} got a job at {building.buildingName}. " +
                          $"Shift: {shift.startHour}:00 – {(shift.startHour + shift.durationHours) % 24}:00");
            }
        }

        currentState = AgentState.Idle;
    }

    void HandleSeekingSchool()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        SchoolBuilding found = null;

        if (CanAttendPreschool())
        {
            Vector3Int? pos = buildingManager.FindNearest<Preschool>(currentCell, s => s.IsEnrollmentOpen());
            if (pos.HasValue)
                found = (SchoolBuilding)buildingManager.GetBuildingAt(pos.Value);
        }
        else if (CanAttendSchool())
        {
            Vector3Int? pos = buildingManager.FindNearest<School>(currentCell, s => s.IsEnrollmentOpen());
            if (pos.HasValue)
                found = (SchoolBuilding)buildingManager.GetBuildingAt(pos.Value);
        }

        if (found != null && found.TryEnroll(this))
        {
            enrolledSchool = found;
            isEnrolled = true;
            EventLog.Log($"{agentName} enrolled at {found.buildingName}.");
            Debug.Log($"{agentName} enrolled at {found.buildingName}.");
        }

        currentState = AgentState.Idle;
    }

    void HandleAtSchool()
    {
        if (enrolledSchool == null || timeManager == null)
        {
            currentState = AgentState.Idle;
            return;
        }
        if (!enrolledSchool.IsInSession(timeManager.CurrentHour))
        {
            enrolledSchool.StudentLeave(this);
            if (hasHome && StartPathTo(homeTile))
                currentState = AgentState.WalkingHome;
            else
                currentState = AgentState.Idle;
        }
    }

    void HandleSeekingFood()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);

        foreach (Vector3Int store in buildingManager.FindAllSorted<BurgerStore>(currentCell, b => b.IsOpen()))
        {
            var path = pathfinder.FindPath(currentCell, store);
            if (path == null) continue;

            foodTile = store;
            StartFollowingPath(path);
            currentState = AgentState.WalkingToFood;
            return;
        }

        currentState = AgentState.Idle;
    }

    void HandleSeekingGroceries()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);

        foreach (Vector3Int market in buildingManager.FindAllSorted<Supermarket>(currentCell, b => b.IsOpen()))
        {
            var path = pathfinder.FindPath(currentCell, market);
            if (path == null) continue;

            groceryTile = market;
            StartFollowingPath(path);
            currentState = AgentState.WalkingToSupermarket;
            return;
        }

        currentState = AgentState.Idle;
    }

    void HandleEating()
    {
        eatTimer -= Time.deltaTime;
        if (eatTimer <= 0f)
        {
            if (hasHome)
            {
                if (StartPathTo(homeTile))
                    currentState = AgentState.WalkingHome;
                else
                    currentState = AgentState.Idle;
            }
            else
                currentState = AgentState.Idle;
        }
    }

    void HandleCooking()
    {
        // Small chance of a kitchen injury each second.
        if (HealthManager.Instance?.minorInjury != null)
            if (Random.value < HealthManager.Instance.cookingInjuryChancePerSecond * Time.deltaTime)
                ContractCondition(HealthManager.Instance.minorInjury);

        cookingTimer -= Time.deltaTime;
        if (cookingTimer <= 0f)
        {
            if (homeDwelling != null && homeDwelling.pantry.Remove(ItemType.Groceries))
            {
                Feed(cookingHungerRestored);

                // Also feed babies and toddlers in the same dwelling — they can't feed themselves.
                foreach (Agent occupant in homeDwelling.DwellingOccupancy)
                {
                    if (occupant != this && occupant.NeedsParentFeed())
                        occupant.Feed(cookingHungerRestored * 0.8f);
                }

                Debug.Log($"{agentName} cooked and ate at home. Pantry has {homeDwelling.pantry.Get(ItemType.Groceries)} groceries left.");
            }
            currentState = AgentState.Idle;
        }
    }

    void HandleSeekingPark()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);

        foreach (Vector3Int park in buildingManager.FindAllSorted<Park>(currentCell))
        {
            var path = pathfinder.FindPath(currentCell, park);
            if (path == null) continue;

            parkTile = park;
            StartFollowingPath(path);
            currentState = AgentState.WalkingToPark;
            return;
        }

        currentState = AgentState.Idle;
    }

    void HandleSeekingHomelessShelter()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);

        // TODO: when HomelessShelter buildings exist, try those first:
        // Vector3Int? shelterPos = buildingManager.FindNearest<HomelessShelter>(currentCell);
        // if (shelterPos.HasValue) { ... }

        // Fallback: find the nearest park to sleep in.
        foreach (Vector3Int park in buildingManager.FindAllSorted<Park>(currentCell))
        {
            var path = pathfinder.FindPath(currentCell, park);
            if (path == null) continue;

            parkTile = park;
            StartFollowingPath(path);
            currentState = AgentState.WalkingToPark;
            return;
        }

        // No shelter or park reachable — sleep rough where they stand.
        Debug.Log($"{agentName} is sleeping rough — no shelter found.");
        currentState = AgentState.Idle;
    }

    void HandleSeekingDoctor()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? pos = buildingManager.FindNearest<DoctorsSurgery>(currentCell, b => b.IsOpen());

        if (pos.HasValue)
        {
            var path = pathfinder.FindPath(currentCell, pos.Value);
            if (path != null)
            {
                medicalTile = pos.Value;
                StartFollowingPath(path);
                currentState = AgentState.WalkingToDoctor;
                return;
            }
        }

        currentState = AgentState.Idle; // No doctor reachable — try again next think cycle.
    }

    void HandleSeekingHospital()
    {
        Vector3Int currentCell = buildingsTilemap.WorldToCell(transform.position);
        Vector3Int? pos = buildingManager.FindNearest<Hospital>(currentCell);

        if (pos.HasValue)
        {
            var path = pathfinder.FindPath(currentCell, pos.Value);
            if (path != null)
            {
                medicalTile = pos.Value;
                StartFollowingPath(path);
                currentState = AgentState.WalkingToHospital;
                return;
            }
        }

        currentState = AgentState.Idle; // No hospital reachable — try again next think cycle.
    }

    // Shared handler for both AtDoctor and AtHospital.
    void HandleAtMedical()
    {
        treatmentTimer -= Time.deltaTime;
        if (treatmentTimer <= 0f)
        {
            if (currentMedicalBuilding != null)
            {
                currentMedicalBuilding.TreatPatient(this);
                currentMedicalBuilding.DischargePatient(this);
                currentMedicalBuilding = null;
            }
            if (hasHome && StartPathTo(homeTile))
                currentState = AgentState.WalkingHome;
            else
                currentState = AgentState.Idle;
        }
    }

    void HandleAtPark()
    {
        // Small chance of a recreational injury at the park.
        if (HealthManager.Instance?.minorInjury != null)
            if (Random.value < HealthManager.Instance.parkInjuryChancePerSecond * Time.deltaTime)
                ContractCondition(HealthManager.Instance.minorInjury);

        visitTimer -= Time.deltaTime;
        if (visitTimer <= 0f)
        {
            loneliness = Mathf.Max(loneliness - visitLonelinessRestored, 0f);
            Debug.Log($"{agentName} enjoyed the park. Loneliness: {loneliness:0}");
            if (hasHome)
            {
                if (StartPathTo(homeTile))
                    currentState = AgentState.WalkingHome;
                else
                    currentState = AgentState.Idle;
            }
            else
                currentState = AgentState.Idle;
        }
    }

    void HandleWorking()
    {
        // Small chance of a work-related injury each second.
        if (HealthManager.Instance?.minorInjury != null)
            if (Random.value < HealthManager.Instance.workInjuryChancePerSecond * Time.deltaTime)
                ContractCondition(HealthManager.Instance.minorInjury);

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

    void HandleRespondingToFire()
    {
        // Path failed or fire was already dealt with — abort.
        if (burningBuilding == null || !burningBuilding.isOnFire)
        {
            burningBuilding = null;
            currentState = AgentState.Patrolling;
            patrolWaitTimer = 2f;
            return;
        }
        // Retry path to the burning building.
        StartPathTo(burningBuilding.gridPosition);
    }

    void HandleExtinguishing()
    {
        extinguishTimer -= Time.deltaTime;
        if (extinguishTimer <= 0f)
        {
            if (burningBuilding != null && burningBuilding.isOnFire)
                burningBuilding.Extinguish();

            burningBuilding = null;
            currentState = AgentState.Patrolling;
            patrolWaitTimer = 3f;
        }
    }

    // Called by FireStation when dispatching this agent to a burning building.
    public void AssignFireResponse(Building target)
    {
        burningBuilding = target;
        isMoving = false; // interrupt current patrol
        currentState = AgentState.RespondingToFire;
        StartPathTo(target.gridPosition);
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
        if (isEnrolled && enrolledSchool != null && dest == enrolledSchool.gridPosition) return true;
        return false;
    }

    // Returns true if a path was found and movement started; false if the destination is unreachable.
    bool StartPathTo(Vector3Int target)
    {
        Vector3Int from = buildingsTilemap.WorldToCell(transform.position);

        // Use cached route if we're starting from the same tile it was computed from.
        if (IsKeyDestination(target) &&
            routeCache.TryGetValue(target, out var cached) &&
            cached.from == from)
        {
            StartFollowingPath(new List<Vector3Int>(cached.path));
            return true;
        }

        var path = pathfinder.FindPath(from, target);
        if (path != null)
        {
            if (IsKeyDestination(target))
                routeCache[target] = (from, new List<Vector3Int>(path));
            StartFollowingPath(path);
            return true;
        }
        return false;
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
                if (employer == null) { currentState = AgentState.Idle; break; }
                employer.WorkerCheckIn(this);
                if (employer is PoliceStation || employer is FireStation)
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
                if (StartPathTo(homeTile))
                    currentState = AgentState.WalkingHome;
                else
                    currentState = AgentState.Idle;
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

            case AgentState.RespondingToFire:
                if (burningBuilding != null && burningBuilding.isOnFire)
                {
                    extinguishTimer = ExtinguishDuration;
                    currentState = AgentState.Extinguishing;
                    EventLog.LogWarning($"{agentName} fighting fire at {burningBuilding.buildingName}.");
                    Debug.Log($"{agentName} arrived at fire at {burningBuilding.buildingName}. Extinguishing...");
                }
                else
                {
                    // Fire already out by the time we arrived
                    burningBuilding = null;
                    currentState = AgentState.Patrolling;
                    patrolWaitTimer = 2f;
                }
                break;

            case AgentState.WalkingToDoctor:
            case AgentState.WalkingToHospital:
                MedicalBuilding medBuilding = buildingManager.GetBuildingAt(medicalTile) as MedicalBuilding;
                if (medBuilding != null && medBuilding.TryAdmit(this))
                {
                    // Mark all matching conditions as receiving treatment.
                    foreach (ActiveCondition ac in conditions)
                        if (medBuilding.CanTreat(ac.data.severity)) ac.receivingTreatment = true;
                    treatmentTimer = medBuilding.treatmentDuration;
                    currentMedicalBuilding = medBuilding;
                    currentState = currentState == AgentState.WalkingToDoctor
                        ? AgentState.AtDoctor : AgentState.AtHospital;
                    Debug.Log($"{agentName} admitted to {medBuilding.buildingName}.");
                }
                else
                {
                    Debug.Log($"{agentName} couldn't be admitted to medical building — retrying.");
                    currentState = AgentState.Idle;
                }
                break;

            case AgentState.WalkingToSchool:
                if (enrolledSchool != null)
                {
                    enrolledSchool.StudentArrive(this);
                    currentState = AgentState.AtSchool;
                    Debug.Log($"{agentName} arrived at {enrolledSchool.buildingName}.");
                }
                else
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
        homelessTimer = 0f;
        homelessRetryTimer = 0f;
    }

    /// <summary>
    /// Evicts this agent from their home. Called when the dwelling is demolished.
    /// Snaps the agent back to Idle so they immediately seek a new home.
    /// </summary>
    public void ClearHome()
    {
        if (homeDwelling != null)
            homeDwelling.DwellingOccupancy.Remove(this);
        homeTile     = Vector3Int.zero;
        homeDwelling = null;
        hasHome      = false;
        homelessRetryTimer = 0f; // start re-checking immediately
        if (currentState == AgentState.WalkingHome ||
            currentState == AgentState.Cooking)
            currentState = AgentState.Idle;
    }

    /// <summary>
    /// Fires this agent from their job. Called when the workplace is demolished.
    /// Snaps the agent back to Idle so they immediately seek new work.
    /// </summary>
    public void LoseJob()
    {
        if (assignedShift != null)
            assignedShift.Unassign(this);
        employer      = null;
        assignedShift = null;
        hasJob        = false;
        if (currentState == AgentState.WalkingToWork ||
            currentState == AgentState.Working)
            currentState = AgentState.Idle;
    }

    public void UnenrollSchool()
    {
        if (enrolledSchool != null)
            enrolledSchool.UnenrollStudent(this);
        enrolledSchool = null;
        isEnrolled = false;
        if (currentState == AgentState.WalkingToSchool || currentState == AgentState.AtSchool)
            currentState = AgentState.Idle;
    }

    public void Feed(float amount)
    {
        hunger = Mathf.Max(hunger - amount, 0f);
    }

    // --- Health API ---

    public void ContractCondition(HealthCondition condition)
    {
        if (condition == null) return;
        if (conditions.Exists(c => c.data == condition)) return; // already infected
        conditions.Add(new ActiveCondition { data = condition });
        Debug.Log($"{agentName} contracted {condition.conditionName}.");
    }

    /// <summary>Removes all active conditions of the given severity (called by medical buildings).</summary>
    public void TreatConditions(ConditionSeverity severity)
    {
        int removed = conditions.RemoveAll(c => c.data.severity == severity);
        if (removed > 0)
            Debug.Log($"{agentName}: {removed} condition(s) of severity {severity} treated.");
    }

    void CheckDiseaseSpread()
    {
        if (conditions.Count == 0) return;
        foreach (Agent other in agentManager.GetAllAgents())
        {
            if (other == this) continue;
            float dist = Vector3.Distance(transform.position, other.transform.position);
            foreach (ActiveCondition ac in conditions)
            {
                if (!ac.symptomatic || !ac.data.isContagious) continue;
                if (dist > ac.data.spreadRadius) continue;
                if (Random.value < ac.data.spreadChancePerExposure)
                    other.ContractCondition(ac.data);
            }
        }
    }

    void ProgressConditions(int day)
    {
        for (int i = conditions.Count - 1; i >= 0; i--)
        {
            ActiveCondition ac = conditions[i];
            ac.daysWithCondition++;

            // End incubation once threshold reached.
            if (!ac.symptomatic && ac.daysWithCondition >= ac.data.incubationDays)
            {
                ac.symptomatic = true;
                EventLog.Log($"{agentName} is showing symptoms of {ac.data.conditionName}.");
            }

            if (!ac.symptomatic) continue;

            // Natural recovery roll.
            if (Random.value < ac.data.naturalRecoveryChancePerDay)
            {
                conditions.RemoveAt(i);
                EventLog.Log($"{agentName} recovered naturally from {ac.data.conditionName}.");
                continue;
            }

            // Progression if untreated.
            if (!ac.receivingTreatment && ac.data.progressesIfUntreated
                && ac.daysWithCondition >= ac.data.daysUntilProgression)
            {
                if (ac.data.progressionCondition != null)
                {
                    EventLog.LogDanger($"{agentName}'s {ac.data.conditionName} has worsened!");
                    conditions.RemoveAt(i);
                    ContractCondition(ac.data.progressionCondition);
                }
                else if (ac.data.severity == ConditionSeverity.Critical)
                {
                    Die();
                    return; // agent is gone — stop processing
                }
            }
        }
    }

    void Die()
    {
        string cause = conditions.Count > 0 ? conditions[0].data.conditionName : "unknown causes";
        EventLog.LogDanger($"{agentName} has died from {cause}.");
        Debug.Log($"{agentName} died from {cause}.");
        if (family != null) family.members.Remove(this);
        if (homeDwelling != null) homeDwelling.DwellingOccupancy.Remove(this);
        if (hasJob) LoseJob();
        if (currentMedicalBuilding != null) currentMedicalBuilding.DischargePatient(this);
        agentManager.RemoveAgent(this);
        Destroy(gameObject);
    }

    void LeaveTown()
    {
        EventLog.Log($"{agentName} gave up waiting for housing and left town.");
        Debug.Log($"{agentName} leaving town — homeless for {homelessTimer:0}s.");
        if (family != null)
            family.members.Remove(this);
        if (homeDwelling != null)
            homeDwelling.DwellingOccupancy.Remove(this);
        if (currentMedicalBuilding != null)
            currentMedicalBuilding.DischargePatient(this);
        agentManager.RemoveAgent(this);
        Destroy(gameObject);
    }

    // Returns true if rent was paid, false if the agent was evicted.
    public bool ChargeRent(int amount)
    {
        if (bankBalance >= amount)
        {
            bankBalance -= amount;
            Debug.Log($"{agentName} paid ${amount} rent. Balance: ${bankBalance}");
            return true;
        }

        EventLog.LogDanger($"{agentName} can't afford rent and has been evicted!");
        Debug.Log($"{agentName} evicted — balance ${bankBalance} couldn't cover ${amount} rent.");
        bankBalance = 0;
        Evict();
        return false;
    }

    public void Evict()
    {
        ClearHome();
        homelessTimer = 0f; // reset so they have full time to find alternative shelter
        currentState = AgentState.SeekingHomelessShelter;
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
        EventLog.LogWarning($"{agentName} is chasing {criminal.agentName}!");
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
        EventLog.LogWarning($"{agentName} is fleeing from police!");
        Debug.Log($"{agentName} is fleeing from the police!");
    }

    // Called by an officer agent when they catch this agent. Fine deducted immediately.
    public void Arrest(int fine)
    {
        isBeingChased = false;
        bankBalance -= fine;
        EventLog.LogDanger($"{agentName} was arrested and fined ${fine}.");
        Debug.Log($"{agentName} was arrested! Lost ${fine} as a fine. Balance: ${bankBalance}");
        currentState = AgentState.Arrested;
        StartCoroutine(ReleaseFromArrest());
    }

    private System.Collections.IEnumerator ReleaseFromArrest()
    {
        yield return new WaitForSeconds(5f);
        EventLog.Log($"{agentName} was released from custody.");
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
    Chasing,
    RespondingToFire,
    Extinguishing,
    SeekingSchool,
    WalkingToSchool,
    AtSchool,
    SeekingHomelessShelter,
    SeekingDoctor,
    WalkingToDoctor,
    AtDoctor,
    SeekingHospital,
    WalkingToHospital,
    AtHospital
}
