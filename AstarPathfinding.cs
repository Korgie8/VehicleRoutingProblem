namespace AstarPathfinding
{
    public class AstarPathfinding
    {
        public static List<Node> FindPathAndReturnPath(int[,] grid, Node startNode, Node endNode)
        {
            PriorityQueue<Node, int> openQueue = new PriorityQueue<Node, int>();

            bool[,] closedSet = new bool[grid.GetLength(0), grid.GetLength(1)];

            Dictionary<(int, int), int> bestGCosts = new Dictionary<(int, int), int>();

            startNode.G = 0;
            startNode.H = Math.Abs(endNode.X - startNode.X) + Math.Abs(endNode.Y - startNode.Y);
            startNode.Parent = null;

            openQueue.Enqueue(startNode, startNode.F);
            bestGCosts[(startNode.X, startNode.Y)] = 0;

            while (openQueue.Count > 0)
            {
                Node currentNode = openQueue.Dequeue();

                if (closedSet[currentNode.X, currentNode.Y])
                    continue;

                closedSet[currentNode.X, currentNode.Y] = true;

                if (currentNode.X == endNode.X && currentNode.Y == endNode.Y)
                    return ReconstructPath(currentNode);

                int[] dirX = { 0, 0, -1, 1 };
                int[] dirY = { -1, 1, 0, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int newX = currentNode.X + dirX[i];
                    int newY = currentNode.Y + dirY[i];

                    // Map Boundaries Check
                    if (newX < 0 || newX >= grid.GetLength(0) || newY < 0 || newY >= grid.GetLength(1))
                        continue;

                    // Wall Check
                    int terrainCost = grid[newX, newY];
                    if (terrainCost == 99) 
                        continue;

                    if (closedSet[newX, newY]) 
                        continue;

                    int tentativeG = currentNode.G + terrainCost;

                    if (!bestGCosts.ContainsKey((newX, newY)) || tentativeG < bestGCosts[(newX, newY)])
                    {
                        bestGCosts[(newX, newY)] = tentativeG;

                        Node neighbor = new Node(newX, newY);
                        neighbor.TerrainCost = terrainCost;
                        neighbor.G = tentativeG;
                        neighbor.H = Math.Abs(endNode.X - neighbor.X) + Math.Abs(endNode.Y - neighbor.Y);
                        neighbor.Parent = currentNode;

                        openQueue.Enqueue(neighbor, neighbor.F);
                    }
                }
            }

            return null; // No path found
        }

        public static int FindPathAndReturnInt(int[,] grid, Node startNode, Node endNode)
        {
            PriorityQueue<Node, int> openQueue = new PriorityQueue<Node, int>();

            bool[,] closedSet = new bool[grid.GetLength(0), grid.GetLength(1)];

            Dictionary<(int, int), int> bestGCosts = new Dictionary<(int, int), int>();

            startNode.G = 0;
            startNode.H = Math.Abs(endNode.X - startNode.X) + Math.Abs(endNode.Y - startNode.Y);
            startNode.Parent = null;

            openQueue.Enqueue(startNode, startNode.F);
            bestGCosts[(startNode.X, startNode.Y)] = 0;

            while (openQueue.Count > 0)
            {
                Node currentNode = openQueue.Dequeue();

                if (closedSet[currentNode.X, currentNode.Y])
                    continue;

                closedSet[currentNode.X, currentNode.Y] = true;

                if (currentNode.X == endNode.X && currentNode.Y == endNode.Y)
                {
                    return currentNode.G;
                }

                int[] dirX = { 0, 0, -1, 1 };
                int[] dirY = { -1, 1, 0, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int newX = currentNode.X + dirX[i];
                    int newY = currentNode.Y + dirY[i];

                    // Map Boundaries Check
                    if (newX < 0 || newX >= grid.GetLength(0) || newY < 0 || newY >= grid.GetLength(1))
                        continue;

                    // Wall Check
                    int terrainCost = grid[newX, newY];
                    if (terrainCost == 99) 
                        continue;

                    if (closedSet[newX, newY]) 
                        continue;

                    int tentativeG = currentNode.G + terrainCost;

                    if (!bestGCosts.ContainsKey((newX, newY)) || tentativeG < bestGCosts[(newX, newY)])
                    {
                        bestGCosts[(newX, newY)] = tentativeG;

                        Node neighbor = new Node(newX, newY);
                        neighbor.TerrainCost = terrainCost;
                        neighbor.G = tentativeG;
                        neighbor.H = Math.Abs(endNode.X - neighbor.X) + Math.Abs(endNode.Y - neighbor.Y);
                        neighbor.Parent = currentNode;

                        openQueue.Enqueue(neighbor, neighbor.F);
                    }
                }
            }

            return -1; // No path found
        }

        public static List<Node> ReconstructPath(Node node)
        {
            List<Node> path = new List<Node>();
            while (node != null)
            {
                path.Add(node);
                node = node.Parent;
            }
            path.Reverse();
            return path;
        }
    }
}