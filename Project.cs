using System;
using System.Collections.Generic;
using System.Linq;
using Pathing = AstarPathfinding.AstarPathfinding;

public class Node
{
    public int X { get; set; }
    public int Y { get; set; }
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
    public Node Start { get; set; }
    public Node End { get; set; }
    public List<Node> Nodes { get; set; }
    public int TotalCost { get; set; }
    public List<int> TerrainCosts { get; set; }

    public Path(List<Node> nodes, int totalCost)
    {
        Nodes = nodes;
        TotalCost = totalCost;
    }
}

public class Order
{
    private static int _idCounter = 1;

    public int Id { get; private set; }
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
    private static int _idCounter = 1;

    public int Id { get; private set; }
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

public class Truck
{
    public main mainApp { get; set; }
    public int Id { get; set; }
    public int CurrentX { get; set; }
    public int CurrentY { get; set; }
    public int FuelCapacity { get; set; }
    public int MaxFuelCapacity { get; set; }
    public int FuelRechargeRate { get; set; }
    public int CurrentStorage { get; set; }
    public int MaxStorage { get; set; }
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
                        CurrentPhysicalPath = new Queue<Node>(Pathing.FindPathAndReturnPath(mainApp.testingGrid, startNode, targetNode));
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
        if (CurrentX == 0 && CurrentY == 0)
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
                targetNode = new Node(0, 0); 
            }

            CurrentPhysicalPath = new Queue<Node>(Pathing.FindPathAndReturnPath(mainApp.testingGrid, startNode, targetNode));
        }
    }

    public Truck(int id, int maxStorage, int maxFuelCapacity = 100, int fuelRechargeRate = 5)
    {
        Id = id;
        MaxStorage = maxStorage;
        MaxFuelCapacity = maxFuelCapacity;
        FuelRechargeRate = fuelRechargeRate;
        CurrentStorage = 0;
        FuelCapacity = maxFuelCapacity;
    }
}

public class SavingsPair
{
    public string OrderA_Key { get; set; }
    public string OrderB_Key { get; set; }
    public int Savings { get; set; }

    public SavingsPair(string a, string b, int savings)
    {
        OrderA_Key = a;
        OrderB_Key = b;
        Savings = savings;
    }
}

public class main
{
    public static void Main()
    {
        main app = new main();
        app.Run();
    }

    // Key: (Start Node ID, End Node ID) -> Distance
    public Dictionary<(string, string), int> DistanceMatrix = new Dictionary<(string, string), int>();
    public Dictionary<string, Truck> RouteStarts = new Dictionary<string, Truck>();
    public Dictionary<string, Truck> RouteEnds = new Dictionary<string, Truck>();
    public HashSet<string> AssignedOrders = new HashSet<string>();
    public List<Order> UnassignedOrders = new List<Order>();

    public List<Order> Orders = new List<Order>();
    public List<Depot> Depots = new List<Depot>();
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

    public void Run()
    {
        // Setup dummy data
        Depots.Add(new Depot(0, 0));
        Orders.Add(new Order(9, 9, 10));
        Orders.Add(new Order(0, 9, 5));
        Orders.Add(new Order(9, 0, 8));

        Trucks.Add(new Truck(1, 30, 100, 5) { mainApp = this });
        Trucks.Add(new Truck(2, 20, 75, 5) { mainApp = this });

        UnassignedOrders.AddRange(Orders);

        BuildDistanceMatrix(testingGrid);

        int currentTick = 0;
    
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
            string a = entry.OrderA_Key;
            string b = entry.OrderB_Key;

            Order orderA = UnassignedOrders.FirstOrDefault(o => $"O{o.Id}" == a);
            Order orderB = UnassignedOrders.FirstOrDefault(o => $"O{o.Id}" == b);

            if (orderA == null || orderB == null) continue;

            bool isAAssigned = AssignedOrders.Contains(a);
            bool isBAssigned = AssignedOrders.Contains(b);

            if (!isAAssigned && !isBAssigned)
            {
                Truck emptyTruck = Trucks.FirstOrDefault(t => t.IsEmpty);
                if (emptyTruck != null)
                {
                    if (emptyTruck.CurrentStorage + orderA.StorageRequired + orderB.StorageRequired <= emptyTruck.MaxStorage)
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
                        }
                    }
                }
            }
            else if (isAAssigned && !isBAssigned)
            {
                if (RouteEnds.TryGetValue(a, out Truck truckEndingWithA))
                {
                    if (truckEndingWithA.CurrentStorage + orderB.StorageRequired <= truckEndingWithA.MaxStorage)
                    {
                        List<Order> testRoute = new List<Order>(truckEndingWithA.AssignedRoute);
                        testRoute.Add(orderB);

                        if (CalculateRouteFuelCost(testRoute) <= truckEndingWithA.MaxFuelCapacity)
                        {
                            truckEndingWithA.AssignedRoute.Add(orderB);
                            truckEndingWithA.CurrentStorage += orderB.StorageRequired;
                            AssignedOrders.Add(b);
                            UnassignedOrders.Remove(orderB);
                            RouteEnds.Remove(a);
                            RouteEnds[b] = truckEndingWithA;
                        }
                    }
                }
                else if (RouteStarts.TryGetValue(a, out Truck truckStartingWithA))
                {
                    if (truckStartingWithA.CurrentStorage + orderB.StorageRequired <= truckStartingWithA.MaxStorage)
                    {
                        List<Order> testRoute = new List<Order>(truckStartingWithA.AssignedRoute);
                        testRoute.Insert(0, orderB);

                        if (CalculateRouteFuelCost(testRoute) <= truckStartingWithA.MaxFuelCapacity)
                        {
                            truckStartingWithA.AssignedRoute.Insert(0, orderB);
                            truckStartingWithA.CurrentStorage += orderB.StorageRequired;
                            AssignedOrders.Add(b);
                            UnassignedOrders.Remove(orderB);
                            RouteStarts.Remove(a);
                            RouteStarts[b] = truckStartingWithA;
                        }
                    }
                }
            }
            else if (!isAAssigned && isBAssigned)
            {
                if (RouteEnds.TryGetValue(b, out Truck truckEndingWithB))
                {
                    if (truckEndingWithB.CurrentStorage + orderA.StorageRequired <= truckEndingWithB.MaxStorage)
                    {
                        List<Order> testRoute = new List<Order>(truckEndingWithB.AssignedRoute);
                        testRoute.Add(orderA);

                        if (CalculateRouteFuelCost(testRoute) <= truckEndingWithB.MaxFuelCapacity)
                        {
                            truckEndingWithB.AssignedRoute.Add(orderA);
                            truckEndingWithB.CurrentStorage += orderA.StorageRequired;
                            AssignedOrders.Add(a);
                            UnassignedOrders.Remove(orderA);
                            RouteEnds.Remove(b);
                            RouteEnds[a] = truckEndingWithB;
                        }
                    }
                }
                else if (RouteStarts.TryGetValue(b, out Truck truckStartingWithB))
                {
                    if (truckStartingWithB.CurrentStorage + orderA.StorageRequired <= truckStartingWithB.MaxStorage)
                    {
                        List<Order> testRoute = new List<Order>(truckStartingWithB.AssignedRoute);
                        testRoute.Insert(0, orderA);

                        if (CalculateRouteFuelCost(testRoute) <= truckStartingWithB.MaxFuelCapacity)
                        {
                            truckStartingWithB.AssignedRoute.Insert(0, orderA);
                            truckStartingWithB.CurrentStorage += orderA.StorageRequired;
                            AssignedOrders.Add(a);
                            UnassignedOrders.Remove(orderA);
                            RouteStarts.Remove(b);
                            RouteStarts[a] = truckStartingWithB;
                        }
                    }
                }
            }
            else if (isAAssigned && isBAssigned)
            {                
                if (RouteEnds.TryGetValue(a, out Truck truckEndingWithA) && RouteStarts.TryGetValue(b, out Truck truckStartingWithB))
                {
                    if (truckEndingWithA.Id != truckStartingWithB.Id) 
                    {
                        if (truckEndingWithA.CurrentStorage + truckStartingWithB.CurrentStorage <= truckEndingWithA.MaxStorage)
                        {
                            List<Order> testRoute = new List<Order>(truckEndingWithA.AssignedRoute);
                            testRoute.AddRange(truckStartingWithB.AssignedRoute);

                            if (CalculateRouteFuelCost(testRoute) <= truckEndingWithA.MaxFuelCapacity)
                            {
                                string endOfTruckB = $"O{truckStartingWithB.LastStop.Id}";
                                truckEndingWithA.AssignedRoute.AddRange(truckStartingWithB.AssignedRoute);
                                truckEndingWithA.CurrentStorage += truckStartingWithB.CurrentStorage;
                                truckStartingWithB.AssignedRoute.Clear();
                                truckStartingWithB.CurrentStorage = 0;
                                RouteEnds.Remove(a);
                                RouteStarts.Remove(b);
                                RouteEnds[endOfTruckB] = truckEndingWithA;
                            }
                        }
                    }
                }
                else if (RouteStarts.TryGetValue(a, out Truck truckStartingWithA) && RouteEnds.TryGetValue(b, out Truck truckEndingWithB))
                {
                    if (truckStartingWithA.Id != truckEndingWithB.Id) 
                    {
                        if (truckEndingWithB.CurrentStorage + truckStartingWithA.CurrentStorage <= truckEndingWithB.MaxStorage)
                        {
                            List<Order> testRoute = new List<Order>(truckEndingWithB.AssignedRoute);
                            testRoute.AddRange(truckStartingWithA.AssignedRoute);

                            if (CalculateRouteFuelCost(testRoute) <= truckEndingWithB.MaxFuelCapacity)
                            {
                                string endOfTruckA = $"O{truckStartingWithA.LastStop.Id}";
                                truckEndingWithB.AssignedRoute.AddRange(truckStartingWithA.AssignedRoute);
                                truckEndingWithB.CurrentStorage += truckStartingWithA.CurrentStorage;
                                truckStartingWithA.AssignedRoute.Clear();
                                truckStartingWithA.CurrentStorage = 0;
                                RouteEnds.Remove(b);
                                RouteStarts.Remove(a);
                                RouteEnds[endOfTruckA] = truckEndingWithB;
                            }
                        }
                    }
                }
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
                Console.WriteLine($"Truck {truck.Id} assigned route: {string.Join(" -> ", truck.AssignedRoute.Select(o => $"O{o.Id}"))}");
            }
        }
    }

    public int CalculateRouteFuelCost(List<Order> proposedRoute)
    {
        if (proposedRoute.Count == 0) return 0;
        
        string depotKey = $"D{Depots[0].Id}";
        int totalFuel = 0;
        
        totalFuel += DistanceMatrix[(depotKey, $"O{proposedRoute[0].Id}")];
        
        for (int i = 0; i < proposedRoute.Count - 1; i++)
        {
            string currentStop = $"O{proposedRoute[i].Id}";
            string nextStop = $"O{proposedRoute[i+1].Id}";
            totalFuel += DistanceMatrix[(currentStop, nextStop)];
        }
        
        totalFuel += DistanceMatrix[($"O{proposedRoute[proposedRoute.Count - 1].Id}", depotKey)];
        
        return totalFuel;
    }

    public void BuildDistanceMatrix(int[,] grid)
    {
        Console.WriteLine("Building Distance Matrix...");

        foreach (var depot in Depots)
        {
            Node depotNode = new Node(depot.X, depot.Y);
            string depotKey = $"D{depot.Id}";

            foreach (var order in Orders)
            {
                Node orderNode = new Node(order.X, order.Y);
                string orderKey = $"O{order.Id}";

                int cost = Pathing.FindPathAndReturnInt(grid, depotNode, orderNode);
                
                if (cost != -1)
                {
                    DistanceMatrix[(depotKey, orderKey)] = cost;
                    DistanceMatrix[(orderKey, depotKey)] = cost;
                }
                else
                {
                    Console.WriteLine($"WARNING: Order {order.Id} is unreachable from Depot {depot.Id}.");
                }
            }
        }

        for (int i = 0; i < Orders.Count; i++)
        {
            Node nodeA = new Node(Orders[i].X, Orders[i].Y);
            string keyA = $"O{Orders[i].Id}";

            for (int j = i + 1; j < Orders.Count; j++)
            {
                Node nodeB = new Node(Orders[j].X, Orders[j].Y);
                string keyB = $"O{Orders[j].Id}";

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
        foreach (var depot in Depots)
        {
            string depotKey = $"D{depot.Id}";

            for (int i = 0; i < UnassignedOrders.Count; i++)
            {
                string orderAKey = $"O{UnassignedOrders[i].Id}";

                for (int j = i + 1; j < UnassignedOrders.Count; j++)
                {
                    string orderBKey = $"O{UnassignedOrders[j].Id}";

                    if (DistanceMatrix.TryGetValue((orderAKey, orderBKey), out int costAB) &&
                        DistanceMatrix.TryGetValue((orderAKey, depotKey), out int costADepot) &&
                        DistanceMatrix.TryGetValue((orderBKey, depotKey), out int costBDepot))
                    {
                        int savings = (costADepot + costBDepot) - costAB;
                        savingsList.Add(new SavingsPair(orderAKey, orderBKey, savings));
                    }
                }
            }
        }

        return savingsList.OrderByDescending(s => s.Savings).ToList();
    }
}