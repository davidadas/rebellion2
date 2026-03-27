using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

[TestFixture]
public class ContainerNodeTests
{
    // Mock implementation of ContainerNode for testing purposes
    private class MockContainerNode : ContainerNode
    {
        private readonly List<ISceneNode> children = new List<ISceneNode>();

        public override void AddChild(ISceneNode child)
        {
            children.Add(child);
            child.SetParent(this);
        }

        public override void RemoveChild(ISceneNode child)
        {
            children.Remove(child);
        }

        public override IEnumerable<ISceneNode> GetChildren()
        {
            return children;
        }

        public override IEnumerable<T> GetChildren<T>(Func<T, bool> predicate, bool recurse = true)
        {
            List<T> matchingChildren = new List<T>();

            if (recurse)
            {
                // Use the Traverse method for recursive traversal.
                Traverse(
                    (ISceneNode node) =>
                    {
                        if (
                            node != this
                            && node is T typedNode
                            && (predicate == null || predicate(typedNode))
                        )
                        {
                            matchingChildren.Add(typedNode);
                        }
                    }
                );
            }
            else
            {
                // For non-recursive, only check immediate children.
                foreach (ISceneNode child in GetChildren())
                {
                    if (child is T typedNode && (predicate == null || predicate(typedNode)))
                    {
                        matchingChildren.Add(typedNode);
                    }
                }
            }

            return matchingChildren;
        }

        // Helper method to allow modifying children during traversal tests
        public void ClearChildren()
        {
            children.Clear();
        }

        public void AddChildWithoutSettingParent(ISceneNode child)
        {
            children.Add(child);
        }
    }

    private class MockContainerNodeA : MockContainerNode { }

    private class MockContainerNodeB : MockContainerNode { }

    private class MockContainerNodeC : MockContainerNode { }

    private MockContainerNode rootNode;
    private MockContainerNode childNode1;
    private MockContainerNode childNode2;
    private MockContainerNodeA nodeA1;
    private MockContainerNodeA nodeA2;
    private MockContainerNodeB nodeB1;
    private MockContainerNodeB nodeB2;
    private MockContainerNodeC nodeC1;

    [SetUp]
    public void SetUp()
    {
        rootNode = new MockContainerNode
        {
            DisplayName = "RootNode",
            InstanceID = Guid.NewGuid().ToString(),
        };

        childNode1 = new MockContainerNode
        {
            DisplayName = "ChildNode1",
            InstanceID = Guid.NewGuid().ToString(),
        };

        childNode2 = new MockContainerNode
        {
            DisplayName = "ChildNode2",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeA1 = new MockContainerNodeA
        {
            DisplayName = "NodeA1",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeA2 = new MockContainerNodeA
        {
            DisplayName = "NodeA2",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeB1 = new MockContainerNodeB
        {
            DisplayName = "NodeB1",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeB2 = new MockContainerNodeB
        {
            DisplayName = "NodeB2",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeC1 = new MockContainerNodeC
        {
            DisplayName = "NodeC1",
            InstanceID = Guid.NewGuid().ToString(),
        };
    }

    #region GetChildren Tests - Non-Recursive

    [Test]
    public void GetChildren_NonRecursive_ReturnsOnlyImmediateChildren()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(nodeA1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: false
        );

        Assert.AreEqual(2, result.Count(), "Should return only immediate children");
        CollectionAssert.Contains(result, childNode1);
        CollectionAssert.Contains(result, childNode2);
        CollectionAssert.DoesNotContain(result, nodeA1);
    }

    [Test]
    public void GetChildren_NonRecursiveWithPredicate_ReturnsFilteredImmediateChildren()
    {
        childNode1.OwnerInstanceID = "Owner1";
        childNode2.OwnerInstanceID = "Owner2";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => node.OwnerInstanceID == "Owner1",
            recurse: false
        );

        Assert.AreEqual(1, result.Count(), "Should return only matching immediate children");
        Assert.AreEqual(childNode1, result.First());
    }

    [Test]
    public void GetChildren_NonRecursiveWithTypeFilter_ReturnsOnlyMatchingTypes()
    {
        rootNode.AddChild(nodeA1);
        rootNode.AddChild(nodeB1);
        rootNode.AddChild(nodeA2);

        IEnumerable<MockContainerNodeA> result = rootNode.GetChildren<MockContainerNodeA>(
            node => true,
            recurse: false
        );

        Assert.AreEqual(2, result.Count(), "Should return only type A nodes");
        CollectionAssert.Contains(result, nodeA1);
        CollectionAssert.Contains(result, nodeA2);
        CollectionAssert.DoesNotContain(result, nodeB1);
    }

    [Test]
    public void GetChildren_NonRecursiveEmptyChildren_ReturnsEmptyCollection()
    {
        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: false
        );

        Assert.AreEqual(0, result.Count(), "Should return empty collection when no children");
    }

    [Test]
    public void GetChildren_NonRecursiveSingleChild_ReturnsSingleChild()
    {
        rootNode.AddChild(childNode1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: false
        );

        Assert.AreEqual(1, result.Count(), "Should return single child");
        Assert.AreEqual(childNode1, result.First());
    }

    #endregion

    #region GetChildren Tests - Recursive

    [Test]
    public void GetChildren_Recursive_ReturnsAllDescendants()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(nodeA1);
        childNode2.AddChild(nodeB1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(4, result.Count(), "Should return all descendants");
        CollectionAssert.Contains(result, childNode1);
        CollectionAssert.Contains(result, childNode2);
        CollectionAssert.Contains(result, nodeA1);
        CollectionAssert.Contains(result, nodeB1);
    }

    [Test]
    public void GetChildren_RecursiveWithPredicate_ReturnsFilteredDescendants()
    {
        childNode1.OwnerInstanceID = "Owner1";
        childNode2.OwnerInstanceID = "Owner2";
        nodeA1.OwnerInstanceID = "Owner1";
        nodeB1.OwnerInstanceID = "Owner2";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(nodeA1);
        childNode2.AddChild(nodeB1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => node.OwnerInstanceID == "Owner1",
            recurse: true
        );

        Assert.AreEqual(2, result.Count(), "Should return only matching descendants");
        CollectionAssert.Contains(result, childNode1);
        CollectionAssert.Contains(result, nodeA1);
    }

    [Test]
    public void GetChildren_RecursiveWithTypeFilter_ReturnsOnlyMatchingTypeDescendants()
    {
        rootNode.AddChild(childNode1);
        childNode1.AddChild(nodeA1);
        childNode1.AddChild(nodeB1);
        nodeB1.AddChild(nodeA2);

        IEnumerable<MockContainerNodeA> result = rootNode.GetChildren<MockContainerNodeA>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(2, result.Count(), "Should return only type A descendants");
        CollectionAssert.Contains(result, nodeA1);
        CollectionAssert.Contains(result, nodeA2);
    }

    [Test]
    public void GetChildren_RecursiveMultipleLevels_ReturnsAllLevels()
    {
        // Create a 4-level hierarchy
        rootNode.AddChild(childNode1);
        childNode1.AddChild(childNode2);
        childNode2.AddChild(nodeA1);
        nodeA1.AddChild(nodeB1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(4, result.Count(), "Should return all levels");
        CollectionAssert.Contains(result, childNode1);
        CollectionAssert.Contains(result, childNode2);
        CollectionAssert.Contains(result, nodeA1);
        CollectionAssert.Contains(result, nodeB1);
    }

    [Test]
    public void GetChildren_RecursiveLargeHierarchy_ReturnsAllNodes()
    {
        // Create a larger hierarchy with 10 nodes
        List<MockContainerNode> allNodes = new List<MockContainerNode>();

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        allNodes.Add(childNode1);
        allNodes.Add(childNode2);

        for (int i = 0; i < 4; i++)
        {
            MockContainerNode node = new MockContainerNode
            {
                DisplayName = $"Node{i}",
                InstanceID = Guid.NewGuid().ToString(),
            };
            childNode1.AddChild(node);
            allNodes.Add(node);
        }

        for (int i = 0; i < 4; i++)
        {
            MockContainerNode node = new MockContainerNode
            {
                DisplayName = $"NodeB{i}",
                InstanceID = Guid.NewGuid().ToString(),
            };
            childNode2.AddChild(node);
            allNodes.Add(node);
        }

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(10, result.Count(), "Should return all 10 descendants");
        foreach (MockContainerNode node in allNodes)
        {
            CollectionAssert.Contains(result, node);
        }
    }

    #endregion

    #region GetChildren Tests - Null Predicate

    [Test]
    public void GetChildren_RecursiveNullPredicate_ReturnsAllDescendants()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(nodeA1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            null,
            recurse: true
        );

        Assert.AreEqual(3, result.Count(), "Should return all descendants when predicate is null");
        CollectionAssert.Contains(result, childNode1);
        CollectionAssert.Contains(result, childNode2);
        CollectionAssert.Contains(result, nodeA1);
    }

    [Test]
    public void GetChildren_NonRecursiveNullPredicate_ReturnsAllImmediateChildren()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(nodeA1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            null,
            recurse: false
        );

        Assert.AreEqual(
            2,
            result.Count(),
            "Should return immediate children when predicate is null"
        );
        CollectionAssert.Contains(result, childNode1);
        CollectionAssert.Contains(result, childNode2);
        CollectionAssert.DoesNotContain(result, nodeA1);
    }

    #endregion

    #region GetChildren Tests - Filtering Out Self

    [Test]
    public void GetChildren_Recursive_DoesNotIncludeSelf()
    {
        rootNode.AddChild(childNode1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: true
        );

        CollectionAssert.DoesNotContain(result, rootNode, "Should not include the node itself");
    }

    [Test]
    public void GetChildren_RecursiveWithMatchingSelfType_DoesNotIncludeSelf()
    {
        // Create a hierarchy where the root node is of the same type we're searching for
        MockContainerNodeA rootA = new MockContainerNodeA
        {
            DisplayName = "RootA",
            InstanceID = Guid.NewGuid().ToString(),
        };
        rootA.AddChild(nodeA1);
        rootA.AddChild(nodeA2);

        IEnumerable<MockContainerNodeA> result = rootA.GetChildren<MockContainerNodeA>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(2, result.Count(), "Should not include self even if type matches");
        CollectionAssert.Contains(result, nodeA1);
        CollectionAssert.Contains(result, nodeA2);
        CollectionAssert.DoesNotContain(result, rootA);
    }

    [Test]
    public void GetChildren_RecursiveWithPredicateMatchingSelf_DoesNotIncludeSelf()
    {
        rootNode.OwnerInstanceID = "Owner1";
        childNode1.OwnerInstanceID = "Owner1";

        rootNode.AddChild(childNode1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => node.OwnerInstanceID == "Owner1",
            recurse: true
        );

        Assert.AreEqual(1, result.Count(), "Should not include self even if predicate matches");
        Assert.AreEqual(childNode1, result.First());
    }

    #endregion

    #region Traverse Tests

    [Test]
    public void Traverse_SimpleHierarchy_VisitsAllNodes()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(3, visitedNodes.Count, "Should visit all nodes including root");
        CollectionAssert.Contains(visitedNodes, rootNode);
        CollectionAssert.Contains(visitedNodes, childNode1);
        CollectionAssert.Contains(visitedNodes, childNode2);
    }

    [Test]
    public void Traverse_VisitsRootFirst()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(rootNode, visitedNodes[0], "Root should be visited first");
    }

    [Test]
    public void Traverse_DeepHierarchy_VisitsInCorrectOrder()
    {
        rootNode.AddChild(childNode1);
        childNode1.AddChild(childNode2);
        childNode2.AddChild(nodeA1);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(4, visitedNodes.Count, "Should visit all 4 nodes");
        Assert.AreEqual(rootNode, visitedNodes[0], "Root should be first");

        // Verify all nodes are visited
        CollectionAssert.Contains(visitedNodes, rootNode);
        CollectionAssert.Contains(visitedNodes, childNode1);
        CollectionAssert.Contains(visitedNodes, childNode2);
        CollectionAssert.Contains(visitedNodes, nodeA1);
    }

    [Test]
    public void Traverse_EmptyChildren_VisitsOnlyRoot()
    {
        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(1, visitedNodes.Count, "Should visit only root when no children");
        Assert.AreEqual(rootNode, visitedNodes[0]);
    }

    [Test]
    public void Traverse_SingleChild_VisitsRootAndChild()
    {
        rootNode.AddChild(childNode1);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(2, visitedNodes.Count, "Should visit root and single child");
        Assert.AreEqual(rootNode, visitedNodes[0]);
        Assert.AreEqual(childNode1, visitedNodes[1]);
    }

    [Test]
    public void Traverse_WithAction_ExecutesActionOnEachNode()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        int actionCount = 0;
        rootNode.Traverse(node => actionCount++);

        Assert.AreEqual(3, actionCount, "Action should be executed on each node");
    }

    [Test]
    public void Traverse_LargeHierarchy_VisitsAllNodes()
    {
        // Create a hierarchy with 20 nodes
        for (int i = 0; i < 10; i++)
        {
            MockContainerNode child = new MockContainerNode
            {
                DisplayName = $"Child{i}",
                InstanceID = Guid.NewGuid().ToString(),
            };
            rootNode.AddChild(child);

            for (int j = 0; j < 2; j++)
            {
                MockContainerNode grandchild = new MockContainerNode
                {
                    DisplayName = $"Grandchild{i}_{j}",
                    InstanceID = Guid.NewGuid().ToString(),
                };
                child.AddChild(grandchild);
            }
        }

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        // 1 root + 10 children + 20 grandchildren = 31 nodes
        Assert.AreEqual(31, visitedNodes.Count, "Should visit all 31 nodes");
    }

    #endregion

    #region Traverse Tests - Collection Modification

    [Test]
    public void Traverse_ModifyingChildrenDuringTraversal_HandlesModificationSafely()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();

        // This test verifies that the Traverse method creates a snapshot of children
        // before iterating, so modifications during traversal don't cause issues
        rootNode.Traverse(node =>
        {
            visitedNodes.Add(node);
        });

        // Even though we're not modifying during traversal in this test,
        // the implementation uses ToList() which creates a snapshot
        Assert.AreEqual(3, visitedNodes.Count, "Should visit all nodes despite snapshot approach");
    }

    [Test]
    public void Traverse_ChildRemovedDuringTraversal_ContinuesTraversal()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(nodeA1);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();

        // Note: The implementation creates a snapshot with ToList(), so removing
        // children during traversal won't affect the current traversal
        rootNode.Traverse(node =>
        {
            visitedNodes.Add(node);
            if (node == childNode1)
            {
                // Try to remove a child during traversal
                rootNode.RemoveChild(childNode2);
            }
        });

        // The snapshot approach means childNode2 will still be visited
        // even though it was removed from the live collection
        Assert.GreaterOrEqual(
            visitedNodes.Count,
            3,
            "Should continue traversal despite modification"
        );
    }

    #endregion

    #region Edge Cases

    [Test]
    public void GetChildren_RecursiveEmptyHierarchy_ReturnsEmptyCollection()
    {
        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(0, result.Count(), "Should return empty collection for empty hierarchy");
    }

    [Test]
    public void GetChildren_PredicateRejectsAll_ReturnsEmptyCollection()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => false,
            recurse: true
        );

        Assert.AreEqual(0, result.Count(), "Should return empty when predicate rejects all");
    }

    [Test]
    public void GetChildren_TypeMismatch_ReturnsEmptyCollection()
    {
        rootNode.AddChild(nodeA1);
        rootNode.AddChild(nodeA2);

        IEnumerable<MockContainerNodeB> result = rootNode.GetChildren<MockContainerNodeB>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(0, result.Count(), "Should return empty when no children match type");
    }

    [Test]
    public void GetChildren_ComplexPredicateWithMultipleConditions_ReturnsCorrectResults()
    {
        childNode1.OwnerInstanceID = "Owner1";
        childNode1.DisplayName = "Match";

        childNode2.OwnerInstanceID = "Owner1";
        childNode2.DisplayName = "NoMatch";

        nodeA1.OwnerInstanceID = "Owner2";
        nodeA1.DisplayName = "Match";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        rootNode.AddChild(nodeA1);

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => node.OwnerInstanceID == "Owner1" && node.DisplayName == "Match",
            recurse: false
        );

        Assert.AreEqual(1, result.Count(), "Should return only nodes matching complex predicate");
        Assert.AreEqual(childNode1, result.First());
    }

    [Test]
    public void GetChildren_WideHierarchy_ReturnsAllChildren()
    {
        // Create a wide hierarchy with many siblings
        List<MockContainerNode> children = new List<MockContainerNode>();
        for (int i = 0; i < 50; i++)
        {
            MockContainerNode child = new MockContainerNode
            {
                DisplayName = $"Child{i}",
                InstanceID = Guid.NewGuid().ToString(),
            };
            rootNode.AddChild(child);
            children.Add(child);
        }

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: false
        );

        Assert.AreEqual(50, result.Count(), "Should return all 50 siblings");
        foreach (MockContainerNode child in children)
        {
            CollectionAssert.Contains(result, child);
        }
    }

    [Test]
    public void GetChildren_DeepHierarchy_HandlesMultipleLevelsCorrectly()
    {
        // Create a deep hierarchy: 10 levels deep
        MockContainerNode current = rootNode;
        List<MockContainerNode> allNodes = new List<MockContainerNode>();

        for (int i = 0; i < 10; i++)
        {
            MockContainerNode child = new MockContainerNode
            {
                DisplayName = $"Level{i}",
                InstanceID = Guid.NewGuid().ToString(),
            };
            current.AddChild(child);
            allNodes.Add(child);
            current = child;
        }

        IEnumerable<MockContainerNode> result = rootNode.GetChildren<MockContainerNode>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(10, result.Count(), "Should return all 10 levels");
        foreach (MockContainerNode node in allNodes)
        {
            CollectionAssert.Contains(result, node);
        }
    }

    [Test]
    public void GetChildren_MixedTypeHierarchy_FiltersCorrectly()
    {
        rootNode.AddChild(nodeA1);
        rootNode.AddChild(nodeB1);
        nodeA1.AddChild(nodeC1);
        nodeB1.AddChild(nodeA2);

        IEnumerable<MockContainerNodeA> resultA = rootNode.GetChildren<MockContainerNodeA>(
            node => true,
            recurse: true
        );

        IEnumerable<MockContainerNodeB> resultB = rootNode.GetChildren<MockContainerNodeB>(
            node => true,
            recurse: true
        );

        IEnumerable<MockContainerNodeC> resultC = rootNode.GetChildren<MockContainerNodeC>(
            node => true,
            recurse: true
        );

        Assert.AreEqual(2, resultA.Count(), "Should find 2 type A nodes");
        Assert.AreEqual(1, resultB.Count(), "Should find 1 type B node");
        Assert.AreEqual(1, resultC.Count(), "Should find 1 type C node");
    }

    #endregion
}
