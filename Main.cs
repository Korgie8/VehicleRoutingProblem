using System;
using System.Collections.Generic;
using System.Linq;
using Pathing = AstarPathfinding.AstarPathfinding;

# region Data Classes

public readonly struct EntityHandle : IEquatable<EntityHandle>
{
    public readonly uint Value;

    // Pack on creation
    public EntityHandle(byte type, uint id)
    {
        // Top 8 bits for Type, Bottom 24 bits for ID
        Value = ((uint)type << 24) | (id & 0x00FFFFFF);
    }

    // Unpack on the fly (compiled to single CPU instructions)
    public byte Type => (byte)(Value >> 24);
    public uint Id => Value & 0x00FFFFFF;

    // --- Boilerplate for fast equality comparisons ---
    
    public bool Equals(EntityHandle other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EntityHandle other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EntityHandle left, EntityHandle right) => left.Value == right.Value;
    public static bool operator !=(EntityHandle left, EntityHandle right) => left.Value != right.Value;
    
    public override string ToString() => $"{Type}:{Id}";
}

public class Node
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public int TerrainCost { get; set; } = 1;
    public int G { get; set; } // Cost from start
    public int H { get; set; } // Estimated cost to end
    public int F => G + H;     // Total cost
    public Node Parent { get; set; }

    public Node(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public class Path
{
    public Node Start { get; private set; }
    public Node End { get; private set; }
    public List<Node> Nodes { get; private set; }
    public int TotalCost { get; private set; }
    public List<int> TerrainCosts { get; private set; }

    public Path(List<Node> nodes, int totalCost)
    {
        Nodes = nodes;
        TotalCost = totalCost;
    }
}

public class Order
{
    private static uint _idCounter = 1;

    public uint Id { get; private set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int StorageRequired { get; set; }

    public Order(int x, int y, int storageRequired)
    {
        Id = _idCounter++;
        X = x;
        Y = y;
        StorageRequired = storageRequired;
    }
}

public class Depot
{
    private static uint _idCounter = 1;

    public uint Id { get; private set; }
    public int X { get; set; }
    public int Y { get; set; }

    public Depot(int x, int y)
    {
        Id = _idCounter++;
        X = x;
        Y = y;
    }
}

public enum TruckState
{
    Idle,
    Driving,
    Loading,
    Refueling
}

public enum noteType
{
    Depot = 0,
    Order = 1,
}

public class Truck
{
    private static uint _idCounter = 1;

    public main mainApp { get; set; }
    public uint Id { get; private set; }
    public int CurrentX { get; private set; }
    public int CurrentY { get; private set; }
    
    public int FuelCapacity { get; private set; }
    public int MaxFuelCapacity { get; private set; }
    public int FuelRechargeRate { get; private set; }
    
    public int CurrentStorage { get; set; }
    public int MaxStorage { get; private set; }
    
    public List<Order> AssignedRoute { get; set; } = new List<Order>();
    public TruckState State { get; set; } = TruckState.Idle;

    public bool IsEmpty => AssignedRoute.Count == 0;
    public Order FirstStop => AssignedRoute.FirstOrDefault(); 
    public Order LastStop => AssignedRoute.LastOrDefault();

    public Queue<Order> PendingOrders { get; set; } = new Queue<Order>();
    public Queue<Node> CurrentPhysicalPath { get; set; } = new Queue<Node>();
    public int ActionTimer { get; set; } = 0;
    public void Tick()
    {
        switch (State)
        {
            case TruckState.Idle:
                break;

            case TruckState.Loading:
                ActionTimer--;
                if (ActionTimer <= 0)
                {
                    Console.WriteLine($"Truck {Id} finished loading! Hitting the road.");
                    State = TruckState.Driving;

                    if (PendingOrders.Count > 0)
                    {
                        Order nextOrder = PendingOrders.Peek();
                        Node startNode = new Node(CurrentX, CurrentY);
                        Node targetNode = new Node(nextOrder.X, nextOrder.Y);
                        CurrentPhysicalPath = BuildMovementQueue(startNode, targetNode);
                    }
                }
                break;

            case TruckState.Refueling:
                FuelCapacity += FuelRechargeRate;
                if (FuelCapacity >= MaxFuelCapacity)
                {
                    FuelCapacity = MaxFuelCapacity;
                    Console.WriteLine($"Truck {Id} fully fueled! Back to work.");
                    State = TruckState.Idle;
                }
                break;

            case TruckState.Driving:
                if (CurrentPhysicalPath.Count > 0)
                {
                    Node nextStep = CurrentPhysicalPath.Dequeue();
                    CurrentX = nextStep.X;
                    CurrentY = nextStep.Y;
                    FuelCapacity -= nextStep.TerrainCost;

                    Console.WriteLine($"Truck {Id} moving to ({CurrentX}, {CurrentY})");
                }
                else
                {
                    ArrivedAtDestination();
                }
                break;
        }
    }

    private void ArrivedAtDestination()
    {
        if (CurrentX == mainApp.CentralDepot.X && CurrentY == mainApp.CentralDepot.Y)
        {
            Console.WriteLine($"Truck {Id} returned to Depot.");
            State = TruckState.Refueling;

            AssignedRoute.Clear();
            CurrentStorage = 0;
        }
        else
        {
            Console.WriteLine($"Truck {Id} arrived at Order. Starting to unload.");
            
            Order completedOrder = PendingOrders.Dequeue(); 
            
            ActionTimer = completedOrder.StorageRequired / 2; 
            State = TruckState.Loading;

            Node startNode = new Node(CurrentX, CurrentY);

            Node targetNode;
            if (PendingOrders.Count > 0)
            {
                Order nextOrder = PendingOrders.Peek();
                targetNode = new Node(nextOrder.X, nextOrder.Y);
            }
            else
            {
                targetNode = new Node(mainApp.CentralDepot.X, mainApp.CentralDepot.Y); 
            }

            CurrentPhysicalPath = BuildMovementQueue(startNode, targetNode);
        }
    }

    private Queue<Node> BuildMovementQueue(Node start, Node target)
    {
        var path = Pathing.FindPathAndReturnPath(mainApp.testingGrid, start, target);
        if (path == null) return new Queue<Node>();

        // Remove the first node since it's the current position of the truck
        var queue = new Queue<Node>(path);
        if (queue.Count > 0) queue.Dequeue();
        return queue;
    }

    public Truck(int maxStorage, int maxFuelCapacity = 100, int fuelRechargeRate = 5)
    {
        Id = _idCounter++;
        MaxStorage = maxStorage;
        MaxFuelCapacity = maxFuelCapacity;
        FuelRechargeRate = fuelRechargeRate;
        CurrentStorage = 0;
        FuelCapacity = maxFuelCapacity;
    }
}

public class SavingsPair
{
    public EntityHandle OrderA_Key { get; set; }
    public EntityHandle OrderB_Key { get; set; }
    public int Savings { get; set; }

    public SavingsPair(EntityHandle a, EntityHandle b, int savings)
    {
        OrderA_Key = a;
        OrderB_Key = b;
        Savings = savings;
    }
}

# endregion

public class main
{
    # region Data Structures
    public static void Main()
    {
        // Initialize the backend
        main app = new main();
        
        // Uncomment Run and comment the rest of this script to run the backend simulation without visualization
        //app.Run();

        app.Init();

        // Pass it to the visualizer and run
        SimulationVisualizer visualizer = new SimulationVisualizer(app);
        visualizer.Run(); 
    }

    // Key: (Start Node ID, End Node ID) -> Distance
    public Dictionary<(EntityHandle, EntityHandle), int> DistanceMatrix = new Dictionary<(EntityHandle, EntityHandle), int>();
    public Dictionary<EntityHandle, Truck> RouteStarts = new Dictionary<EntityHandle, Truck>();
    public Dictionary<EntityHandle, Truck> RouteEnds = new Dictionary<EntityHandle, Truck>();
    public HashSet<EntityHandle> AssignedOrders = new HashSet<EntityHandle>();
    public List<Order> UnassignedOrders = new List<Order>();

    public List<Order> Orders = new List<Order>();
    public Depot CentralDepot;
    public List<Truck> Trucks = new List<Truck>();
    public List<SavingsPair> sortedSavings = new List<SavingsPair>();
    public int[,] testingGrid = new int[10, 10]
    {
        { 1,  1,  1,  1, 99,  1,  1,  1,  1,  1 },
        { 1,  1,  1,  1, 99,  1,  1,  1,  1,  1 },
        { 1,  1,  1,  1, 99,  1,  1,  1,  1,  1 },
        { 1,  1,  1,  1, 99,  1,  1,  1,  1,  1 },
        { 1,  1,  1,  1, 99,  5,  5,  5,  1,  1 }, 
        { 1,  1,  1,  1, 99,  5,  5,  5,  1,  1 },
        { 1,  1,  1,  1, 99,  5,  5,  5,  1,  1 },
        { 1,  1,  1,  1,  1,  5,  5,  5,  1,  1 }, 
        { 1,  1,  1,  1,  1,  1,  1,  1,  1,  1 }, 
        { 1,  1,  1,  1,  1,  1,  1,  1,  1,  1 }
    };

    public int CurrentTick { get; private set; } = 0;
    public bool IsSimulationFinished { get; private set; } = false;

    # endregion

    // Changed to Init for visual layer switch to Run and uncomment the while loop for backend simulation
    public void Init()
    // public void Run()
    {
        // Setup dummy data
        CentralDepot = new Depot(0, 0);
        
        Orders.Add(new Order(9, 9, 10));
        Orders.Add(new Order(0, 9, 5));
        Orders.Add(new Order(9, 0, 8));
        Orders.Add(new Order(2, 7, 20));
        Orders.Add(new Order(9, 1, 30));
        Orders.Add(new Order(2, 5, 15));

        Trucks.Add(new Truck(30, 100, 5) { mainApp = this });
        Trucks.Add(new Truck(20 , 75, 5) { mainApp = this });

        UnassignedOrders.AddRange(Orders);

        BuildDistanceMatrix(testingGrid);

        /*int currentTick = 0;
    
        while (true)
        {
            currentTick++;

            DispatchWave();

            foreach (var truck in Trucks)
            {
                truck.Tick();
            }

            if (UnassignedOrders.Count == 0 && Trucks.All(t => t.State == TruckState.Idle))
            {
                Console.WriteLine($"All orders delivered! Shift ended at tick {currentTick}.");
                break; 
            }

            System.Threading.Thread.Sleep(100);
        }*/
    }

    public void TickSimulation()
    {
        if (IsSimulationFinished) return;

        CurrentTick++;
        DispatchWave();

        foreach (var truck in Trucks)
        {
            truck.Tick();
        }

        if (UnassignedOrders.Count == 0 && Trucks.All(t => t.State == TruckState.Idle))
        {
            Console.WriteLine($"All orders delivered! Shift ended at tick {CurrentTick}.");
            IsSimulationFinished = true;
        }
    }

    public void DispatchWave()
    {

        List<Truck> availableTrucks = Trucks.Where(t => t.State == TruckState.Idle).ToList();

        if (availableTrucks.Count == 0 || UnassignedOrders.Count == 0) return;

        Console.WriteLine($"\n--- Dispatching new wave! {availableTrucks.Count} trucks available, {UnassignedOrders.Count} orders pending ---");

        RouteStarts.Clear();
        RouteEnds.Clear();
        AssignedOrders.Clear();

        sortedSavings = CalculateSavings();

        foreach (var entry in sortedSavings)
        {
            EntityHandle a = entry.OrderA_Key;
            EntityHandle b = entry.OrderB_Key;

            Order orderA = UnassignedOrders.FirstOrDefault(o => o.Id == a.Id);
            Order orderB = UnassignedOrders.FirstOrDefault(o => o.Id == b.Id);

            if (orderA == null || orderB == null) continue;

            bool isAAssigned = AssignedOrders.Contains(a);
            bool isBAssigned = AssignedOrders.Contains(b);

            if (!isAAssigned && !isBAssigned)
            {
                TryCreateNewRoute(orderA, orderB, a, b);
            }
            else if (isAAssigned && !isBAssigned)
            {
                TryAppendToRoute(orderA, orderB, a, b);
            }
            else if (!isAAssigned && isBAssigned)
            {
                TryAppendToRoute(orderB, orderA, b, a); 
            }
            else if (isAAssigned && isBAssigned)
            {                
                TryMergeExistingRoutes(a, b);
            }
        }

        for (int i = UnassignedOrders.Count - 1; i >= 0; i--)
        {
            Order leftoverOrder = UnassignedOrders[i];

            // Find any idle truck that has enough space for this single order
            Truck availableTruck = availableTrucks.FirstOrDefault(t => 
                t.CurrentStorage + leftoverOrder.StorageRequired <= t.MaxStorage);

            if (availableTruck != null)
            {
                availableTruck.AssignedRoute.Add(leftoverOrder);
                availableTruck.CurrentStorage += leftoverOrder.StorageRequired;
                UnassignedOrders.RemoveAt(i);
            }
        }

        foreach (var truck in availableTrucks)
        {
            if (!truck.IsEmpty)
            {
                foreach(var order in truck.AssignedRoute)
                {
                    truck.PendingOrders.Enqueue(order);
                }

                truck.ActionTimer = truck.CurrentStorage / 2;
                truck.State = TruckState.Loading;
                Console.WriteLine($"Truck {truck.Id} assigned route: {string.Join(" -> ", truck.AssignedRoute.Select(handle => $"O{handle.Id}"))}");
            }
        }
    }

    private bool TryCreateNewRoute(Order orderA, Order orderB, EntityHandle a, EntityHandle b)
    {
        Truck emptyTruck = Trucks.FirstOrDefault(t => t.IsEmpty);
        if (emptyTruck != null && emptyTruck.CurrentStorage + orderA.StorageRequired + orderB.StorageRequired <= emptyTruck.MaxStorage)
        {
            List<Order> testRoute = new List<Order> { orderA, orderB };
            if (CalculateRouteFuelCost(testRoute) <= emptyTruck.MaxFuelCapacity)
            {
                emptyTruck.AssignedRoute.Add(orderA);
                emptyTruck.AssignedRoute.Add(orderB);
                emptyTruck.CurrentStorage += (orderA.StorageRequired + orderB.StorageRequired);
                AssignedOrders.Add(a);
                AssignedOrders.Add(b);
                UnassignedOrders.Remove(orderA);
                UnassignedOrders.Remove(orderB);
                RouteStarts[a] = emptyTruck;
                RouteEnds[b] = emptyTruck;
                return true;
            }
        }
        return false;
    }

    private bool TryAppendToRoute(Order assignedOrder, Order unassignedOrder, EntityHandle assignedKey, EntityHandle unassignedKey)
    {
        // Try appending to the END of a route
        if (RouteEnds.TryGetValue(assignedKey, out Truck truckEndingWithAssigned) && 
            truckEndingWithAssigned.CurrentStorage + unassignedOrder.StorageRequired <= truckEndingWithAssigned.MaxStorage)
        {
            List<Order> testRoute = new List<Order>(truckEndingWithAssigned.AssignedRoute) { unassignedOrder };
            if (CalculateRouteFuelCost(testRoute) <= truckEndingWithAssigned.MaxFuelCapacity)
            {
                truckEndingWithAssigned.AssignedRoute.Add(unassignedOrder);
                truckEndingWithAssigned.CurrentStorage += unassignedOrder.StorageRequired;
                AssignedOrders.Add(unassignedKey);
                UnassignedOrders.Remove(unassignedOrder);
                RouteEnds.Remove(assignedKey);
                RouteEnds[unassignedKey] = truckEndingWithAssigned;
                return true;
            }
        }

        // Try prepending to the START of a route
        else if (RouteStarts.TryGetValue(assignedKey, out Truck truckStartingWithAssigned) && 
                truckStartingWithAssigned.CurrentStorage + unassignedOrder.StorageRequired <= truckStartingWithAssigned.MaxStorage)
        {
            List<Order> testRoute = new List<Order>(truckStartingWithAssigned.AssignedRoute);
            testRoute.Insert(0, unassignedOrder);
            if (CalculateRouteFuelCost(testRoute) <= truckStartingWithAssigned.MaxFuelCapacity)
            {
                truckStartingWithAssigned.AssignedRoute.Insert(0, unassignedOrder);
                truckStartingWithAssigned.CurrentStorage += unassignedOrder.StorageRequired;
                AssignedOrders.Add(unassignedKey);
                UnassignedOrders.Remove(unassignedOrder);
                RouteStarts.Remove(assignedKey);
                RouteStarts[unassignedKey] = truckStartingWithAssigned;
                return true;
            }
        }
        return false;
    }

    private bool TryMergeExistingRoutes(EntityHandle keyA, EntityHandle keyB)
    {
        if (RouteEnds.TryGetValue(keyA, out Truck truckA) && RouteStarts.TryGetValue(keyB, out Truck truckB) && truckA.Id != truckB.Id)
        {
            if (truckA.CurrentStorage + truckB.CurrentStorage <= truckA.MaxStorage)
            {
                List<Order> testRoute = new List<Order>(truckA.AssignedRoute);
                testRoute.AddRange(truckB.AssignedRoute);
                if (CalculateRouteFuelCost(testRoute) <= truckA.MaxFuelCapacity)
                {
                    EntityHandle endOfTruckB = new EntityHandle(type: 1, id: truckB.LastStop.Id);
                    truckA.AssignedRoute.AddRange(truckB.AssignedRoute);
                    truckA.CurrentStorage += truckB.CurrentStorage;
                    truckB.AssignedRoute.Clear();
                    truckB.CurrentStorage = 0;
                    RouteEnds.Remove(keyA);
                    RouteStarts.Remove(keyB);
                    RouteEnds[endOfTruckB] = truckA;
                    return true;
                }
            }
        }
        else if (RouteStarts.TryGetValue(keyA, out truckA) && RouteEnds.TryGetValue(keyB, out truckB) && truckA.Id != truckB.Id)
        {
            if (truckB.CurrentStorage + truckA.CurrentStorage <= truckB.MaxStorage)
            {
                List<Order> testRoute = new List<Order>(truckB.AssignedRoute);
                testRoute.AddRange(truckA.AssignedRoute);
                if (CalculateRouteFuelCost(testRoute) <= truckB.MaxFuelCapacity)
                {
                    EntityHandle endOfTruckA = new EntityHandle(type: 1, id: truckA.LastStop.Id);
                    truckB.AssignedRoute.AddRange(truckA.AssignedRoute);
                    truckB.CurrentStorage += truckA.CurrentStorage;
                    truckA.AssignedRoute.Clear();
                    truckA.CurrentStorage = 0;
                    RouteEnds.Remove(keyB);
                    RouteStarts.Remove(keyA);
                    RouteEnds[endOfTruckA] = truckB;
                    return true;
                }
            }
        }
        return false;
    }

    public int CalculateRouteFuelCost(List<Order> proposedRoute)
    {
        if (proposedRoute.Count == 0) return 0;
        
        EntityHandle depotKey = new EntityHandle(type: 0, id: CentralDepot.Id);
        int totalFuel = 0;
        
        totalFuel += DistanceMatrix[(depotKey, new EntityHandle(type: 1, id: proposedRoute[0].Id))];
        
        for (int i = 0; i < proposedRoute.Count - 1; i++)
        {
            EntityHandle currentStop = new EntityHandle(type: 1, id: proposedRoute[i].Id);
            EntityHandle nextStop = new EntityHandle(type: 1, id: proposedRoute[i+1].Id);
            totalFuel += DistanceMatrix[(currentStop, nextStop)];
        }
        
        totalFuel += DistanceMatrix[(new EntityHandle(type: 1, id: proposedRoute[proposedRoute.Count - 1].Id), depotKey)];
        
        return totalFuel;
    }

    public void BuildDistanceMatrix(int[,] grid)
    {
        Console.WriteLine("Building Distance Matrix...");

        EntityHandle depotKey = new EntityHandle(type: 0, id: CentralDepot.Id);
        Node depotNode = new Node(CentralDepot.X, CentralDepot.Y);

        foreach (var order in Orders)
        {
            Node orderNode = new Node(order.X, order.Y);
            EntityHandle orderKey = new EntityHandle(type: 1, id: order.Id);
            int cost = Pathing.FindPathAndReturnInt(grid, depotNode, orderNode);
            
            if (cost != -1)
            {
                DistanceMatrix[(depotKey, orderKey)] = cost;
                DistanceMatrix[(orderKey, depotKey)] = cost;
            }
        }

        for (int i = 0; i < Orders.Count; i++)
        {
            Node nodeA = new Node(Orders[i].X, Orders[i].Y);
            EntityHandle keyA = new EntityHandle(type: 1, id: Orders[i].Id);

            for (int j = i + 1; j < Orders.Count; j++)
            {
                Node nodeB = new Node(Orders[j].X, Orders[j].Y);
                EntityHandle keyB = new EntityHandle(type: 1, id: Orders[j].Id);

                int cost = Pathing.FindPathAndReturnInt(grid, nodeA, nodeB);

                if (cost != -1)
                {
                    DistanceMatrix[(keyA, keyB)] = cost;
                    DistanceMatrix[(keyB, keyA)] = cost;
                }
            }
        }

        Console.WriteLine($"Matrix complete. {DistanceMatrix.Count} routes cached.");
    }

    public List<SavingsPair> CalculateSavings()
    {
        List<SavingsPair> savingsList = new List<SavingsPair>();
        EntityHandle depotKey = new EntityHandle(type: 0, id: CentralDepot.Id);

        for (int i = 0; i < UnassignedOrders.Count; i++)
        {
            EntityHandle orderAKey = new EntityHandle(type: 1, id: UnassignedOrders[i].Id);


            for (int j = i + 1; j < UnassignedOrders.Count; j++)
            {
                EntityHandle orderBKey = new EntityHandle(type: 1, id: UnassignedOrders[j].Id);

                if (DistanceMatrix.TryGetValue((orderAKey, orderBKey), out int costAB) &&
                    DistanceMatrix.TryGetValue((orderAKey, depotKey), out int costADepot) &&
                    DistanceMatrix.TryGetValue((orderBKey, depotKey), out int costBDepot))
                {
                    int savings = (costADepot + costBDepot) - costAB;
                    savingsList.Add(new SavingsPair(orderAKey, orderBKey, savings));
                }
            }
        }

        return savingsList.OrderByDescending(s => s.Savings).ToList();
    }
}