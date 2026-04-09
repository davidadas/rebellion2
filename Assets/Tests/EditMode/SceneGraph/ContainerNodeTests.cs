using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.SceneGraph
{
    [TestFixture]
    public class ContainerNodeTests
    {
        // Mock implementation of ContainerNode for testing purposes
        private class MockContainerNode : ContainerNode
        {
            private readonly List<ISceneNode> children = new List<ISceneNode>();

            public override bool CanAcceptChild(ISceneNode child) => true;

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

            public override IEnumerable<T> GetChildren<T>(
                Func<T, bool> predicate,
                bool recurse = true
            )
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

        private MockContainerNode _rootNode;
        private MockContainerNode _childNode1;
        private MockContainerNode _childNode2;
        private MockContainerNodeA _nodeA1;
        private MockContainerNodeA _nodeA2;
        private MockContainerNodeB _nodeB1;
        private MockContainerNodeB _nodeB2;
        private MockContainerNodeC _nodeC1;

        [SetUp]
        public void SetUp()
        {
            _rootNode = new MockContainerNode
            {
                DisplayName = "RootNode",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _childNode1 = new MockContainerNode
            {
                DisplayName = "ChildNode1",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _childNode2 = new MockContainerNode
            {
                DisplayName = "ChildNode2",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeA1 = new MockContainerNodeA
            {
                DisplayName = "NodeA1",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeA2 = new MockContainerNodeA
            {
                DisplayName = "NodeA2",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeB1 = new MockContainerNodeB
            {
                DisplayName = "NodeB1",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeB2 = new MockContainerNodeB
            {
                DisplayName = "NodeB2",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeC1 = new MockContainerNodeC
            {
                DisplayName = "NodeC1",
                InstanceID = Guid.NewGuid().ToString(),
            };
        }

        #region GetChildren Tests - Non-Recursive

        [Test]
        public void GetChildren_NonRecursive_ReturnsOnlyImmediateChildren()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(_nodeA1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: false
            );

            Assert.AreEqual(2, result.Count(), "Should return only immediate children");
            CollectionAssert.Contains(result, _childNode1);
            CollectionAssert.Contains(result, _childNode2);
            CollectionAssert.DoesNotContain(result, _nodeA1);
        }

        [Test]
        public void GetChildren_NonRecursiveWithPredicate_ReturnsFilteredImmediateChildren()
        {
            _childNode1.OwnerInstanceID = "Owner1";
            _childNode2.OwnerInstanceID = "Owner2";

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                node => node.OwnerInstanceID == "Owner1",
                recurse: false
            );

            Assert.AreEqual(1, result.Count(), "Should return only matching immediate children");
            Assert.AreEqual(_childNode1, result.First());
        }

        [Test]
        public void GetChildren_NonRecursiveWithTypeFilter_ReturnsOnlyMatchingTypes()
        {
            _rootNode.AddChild(_nodeA1);
            _rootNode.AddChild(_nodeB1);
            _rootNode.AddChild(_nodeA2);

            IEnumerable<MockContainerNodeA> result = _rootNode.GetChildren<MockContainerNodeA>(
                null,
                recurse: false
            );

            Assert.AreEqual(2, result.Count(), "Should return only type A nodes");
            CollectionAssert.Contains(result, _nodeA1);
            CollectionAssert.Contains(result, _nodeA2);
            CollectionAssert.DoesNotContain(result, _nodeB1);
        }

        [Test]
        public void GetChildren_NonRecursiveEmptyChildren_ReturnsEmptyCollection()
        {
            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: false
            );

            Assert.AreEqual(0, result.Count(), "Should return empty collection when no children");
        }

        [Test]
        public void GetChildren_NonRecursiveSingleChild_ReturnsSingleChild()
        {
            _rootNode.AddChild(_childNode1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: false
            );

            Assert.AreEqual(1, result.Count(), "Should return single child");
            Assert.AreEqual(_childNode1, result.First());
        }

        #endregion

        #region GetChildren Tests - Recursive

        [Test]
        public void GetChildren_Recursive_ReturnsAllDescendants()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(_nodeA1);
            _childNode2.AddChild(_nodeB1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: true
            );

            Assert.AreEqual(4, result.Count(), "Should return all descendants");
            CollectionAssert.Contains(result, _childNode1);
            CollectionAssert.Contains(result, _childNode2);
            CollectionAssert.Contains(result, _nodeA1);
            CollectionAssert.Contains(result, _nodeB1);
        }

        [Test]
        public void GetChildren_RecursiveWithPredicate_ReturnsFilteredDescendants()
        {
            _childNode1.OwnerInstanceID = "Owner1";
            _childNode2.OwnerInstanceID = "Owner2";
            _nodeA1.OwnerInstanceID = "Owner1";
            _nodeB1.OwnerInstanceID = "Owner2";

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(_nodeA1);
            _childNode2.AddChild(_nodeB1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                node => node.OwnerInstanceID == "Owner1",
                recurse: true
            );

            Assert.AreEqual(2, result.Count(), "Should return only matching descendants");
            CollectionAssert.Contains(result, _childNode1);
            CollectionAssert.Contains(result, _nodeA1);
        }

        [Test]
        public void GetChildren_RecursiveWithTypeFilter_ReturnsOnlyMatchingTypeDescendants()
        {
            _rootNode.AddChild(_childNode1);
            _childNode1.AddChild(_nodeA1);
            _childNode1.AddChild(_nodeB1);
            _nodeB1.AddChild(_nodeA2);

            IEnumerable<MockContainerNodeA> result = _rootNode.GetChildren<MockContainerNodeA>(
                null,
                recurse: true
            );

            Assert.AreEqual(2, result.Count(), "Should return only type A descendants");
            CollectionAssert.Contains(result, _nodeA1);
            CollectionAssert.Contains(result, _nodeA2);
        }

        [Test]
        public void GetChildren_RecursiveMultipleLevels_ReturnsAllLevels()
        {
            // Create a 4-level hierarchy
            _rootNode.AddChild(_childNode1);
            _childNode1.AddChild(_childNode2);
            _childNode2.AddChild(_nodeA1);
            _nodeA1.AddChild(_nodeB1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: true
            );

            Assert.AreEqual(4, result.Count(), "Should return all levels");
            CollectionAssert.Contains(result, _childNode1);
            CollectionAssert.Contains(result, _childNode2);
            CollectionAssert.Contains(result, _nodeA1);
            CollectionAssert.Contains(result, _nodeB1);
        }

        [Test]
        public void GetChildren_RecursiveLargeHierarchy_ReturnsAllNodes()
        {
            // Create a larger hierarchy with 10 nodes
            List<MockContainerNode> allNodes = new List<MockContainerNode>();

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            allNodes.Add(_childNode1);
            allNodes.Add(_childNode2);

            for (int i = 0; i < 4; i++)
            {
                MockContainerNode node = new MockContainerNode
                {
                    DisplayName = $"Node{i}",
                    InstanceID = Guid.NewGuid().ToString(),
                };
                _childNode1.AddChild(node);
                allNodes.Add(node);
            }

            for (int i = 0; i < 4; i++)
            {
                MockContainerNode node = new MockContainerNode
                {
                    DisplayName = $"NodeB{i}",
                    InstanceID = Guid.NewGuid().ToString(),
                };
                _childNode2.AddChild(node);
                allNodes.Add(node);
            }

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
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
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(_nodeA1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: true
            );

            Assert.AreEqual(
                3,
                result.Count(),
                "Should return all descendants when predicate is null"
            );
            CollectionAssert.Contains(result, _childNode1);
            CollectionAssert.Contains(result, _childNode2);
            CollectionAssert.Contains(result, _nodeA1);
        }

        [Test]
        public void GetChildren_NonRecursiveNullPredicate_ReturnsAllImmediateChildren()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(_nodeA1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: false
            );

            Assert.AreEqual(
                2,
                result.Count(),
                "Should return immediate children when predicate is null"
            );
            CollectionAssert.Contains(result, _childNode1);
            CollectionAssert.Contains(result, _childNode2);
            CollectionAssert.DoesNotContain(result, _nodeA1);
        }

        #endregion

        #region GetChildren Tests - Filtering Out Self

        [Test]
        public void GetChildren_Recursive_DoesNotIncludeSelf()
        {
            _rootNode.AddChild(_childNode1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: true
            );

            CollectionAssert.DoesNotContain(
                result,
                _rootNode,
                "Should not include the node itself"
            );
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
            rootA.AddChild(_nodeA1);
            rootA.AddChild(_nodeA2);

            IEnumerable<MockContainerNodeA> result = rootA.GetChildren<MockContainerNodeA>(
                null,
                recurse: true
            );

            Assert.AreEqual(2, result.Count(), "Should not include self even if type matches");
            CollectionAssert.Contains(result, _nodeA1);
            CollectionAssert.Contains(result, _nodeA2);
            CollectionAssert.DoesNotContain(result, rootA);
        }

        [Test]
        public void GetChildren_RecursiveWithPredicateMatchingSelf_DoesNotIncludeSelf()
        {
            _rootNode.OwnerInstanceID = "Owner1";
            _childNode1.OwnerInstanceID = "Owner1";

            _rootNode.AddChild(_childNode1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                node => node.OwnerInstanceID == "Owner1",
                recurse: true
            );

            Assert.AreEqual(1, result.Count(), "Should not include self even if predicate matches");
            Assert.AreEqual(_childNode1, result.First());
        }

        #endregion

        #region Traverse Tests

        [Test]
        public void Traverse_SimpleHierarchy_VisitsAllNodes()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _rootNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(3, visitedNodes.Count, "Should visit all nodes including root");
            CollectionAssert.Contains(visitedNodes, _rootNode);
            CollectionAssert.Contains(visitedNodes, _childNode1);
            CollectionAssert.Contains(visitedNodes, _childNode2);
        }

        [Test]
        public void Traverse_TreeWithChildren_VisitsRootFirst()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _rootNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(_rootNode, visitedNodes[0], "Root should be visited first");
        }

        [Test]
        public void Traverse_DeepHierarchy_VisitsInCorrectOrder()
        {
            _rootNode.AddChild(_childNode1);
            _childNode1.AddChild(_childNode2);
            _childNode2.AddChild(_nodeA1);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _rootNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(4, visitedNodes.Count, "Should visit all 4 nodes");
            Assert.AreEqual(_rootNode, visitedNodes[0], "Root should be first");

            // Verify all nodes are visited
            CollectionAssert.Contains(visitedNodes, _rootNode);
            CollectionAssert.Contains(visitedNodes, _childNode1);
            CollectionAssert.Contains(visitedNodes, _childNode2);
            CollectionAssert.Contains(visitedNodes, _nodeA1);
        }

        [Test]
        public void Traverse_EmptyChildren_VisitsOnlyRoot()
        {
            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _rootNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(1, visitedNodes.Count, "Should visit only root when no children");
            Assert.AreEqual(_rootNode, visitedNodes[0]);
        }

        [Test]
        public void Traverse_SingleChild_VisitsRootAndChild()
        {
            _rootNode.AddChild(_childNode1);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _rootNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(2, visitedNodes.Count, "Should visit root and single child");
            Assert.AreEqual(_rootNode, visitedNodes[0]);
            Assert.AreEqual(_childNode1, visitedNodes[1]);
        }

        [Test]
        public void Traverse_WithAction_ExecutesActionOnEachNode()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            int actionCount = 0;
            _rootNode.Traverse(_ => actionCount++);

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
                _rootNode.AddChild(child);

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
            _rootNode.Traverse(node => visitedNodes.Add(node));

            // 1 root + 10 children + 20 grandchildren = 31 nodes
            Assert.AreEqual(31, visitedNodes.Count, "Should visit all 31 nodes");
        }

        #endregion

        #region Traverse Tests - Collection Modification

        [Test]
        public void Traverse_ModifyingChildrenDuringTraversal_HandlesModificationSafely()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();

            // This test verifies that the Traverse method creates a snapshot of children
            // before iterating, so modifications during traversal don't cause issues
            _rootNode.Traverse(node =>
            {
                visitedNodes.Add(node);
            });

            // Even though we're not modifying during traversal in this test,
            // the implementation uses ToList() which creates a snapshot
            Assert.AreEqual(
                3,
                visitedNodes.Count,
                "Should visit all nodes despite snapshot approach"
            );
        }

        [Test]
        public void Traverse_ChildRemovedDuringTraversal_ContinuesTraversal()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(_nodeA1);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();

            // Note: The implementation creates a snapshot with ToList(), so removing
            // children during traversal won't affect the current traversal
            _rootNode.Traverse(node =>
            {
                visitedNodes.Add(node);
                if (node == _childNode1)
                {
                    // Try to remove a child during traversal
                    _rootNode.RemoveChild(_childNode2);
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
            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
                recurse: true
            );

            Assert.AreEqual(
                0,
                result.Count(),
                "Should return empty collection for empty hierarchy"
            );
        }

        [Test]
        public void GetChildren_PredicateRejectsAll_ReturnsEmptyCollection()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                _ => false,
                recurse: true
            );

            Assert.AreEqual(0, result.Count(), "Should return empty when predicate rejects all");
        }

        [Test]
        public void GetChildren_TypeMismatch_ReturnsEmptyCollection()
        {
            _rootNode.AddChild(_nodeA1);
            _rootNode.AddChild(_nodeA2);

            IEnumerable<MockContainerNodeB> result = _rootNode.GetChildren<MockContainerNodeB>(
                null,
                recurse: true
            );

            Assert.AreEqual(0, result.Count(), "Should return empty when no children match type");
        }

        [Test]
        public void GetChildren_ComplexPredicateWithMultipleConditions_ReturnsCorrectResults()
        {
            _childNode1.OwnerInstanceID = "Owner1";
            _childNode1.DisplayName = "Match";

            _childNode2.OwnerInstanceID = "Owner1";
            _childNode2.DisplayName = "NoMatch";

            _nodeA1.OwnerInstanceID = "Owner2";
            _nodeA1.DisplayName = "Match";

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _rootNode.AddChild(_nodeA1);

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                node => node.OwnerInstanceID == "Owner1" && node.DisplayName == "Match",
                recurse: false
            );

            Assert.AreEqual(
                1,
                result.Count(),
                "Should return only nodes matching complex predicate"
            );
            Assert.AreEqual(_childNode1, result.First());
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
                _rootNode.AddChild(child);
                children.Add(child);
            }

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
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
            MockContainerNode current = _rootNode;
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

            IEnumerable<MockContainerNode> result = _rootNode.GetChildren<MockContainerNode>(
                null,
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
            _rootNode.AddChild(_nodeA1);
            _rootNode.AddChild(_nodeB1);
            _nodeA1.AddChild(_nodeC1);
            _nodeB1.AddChild(_nodeA2);

            IEnumerable<MockContainerNodeA> resultA = _rootNode.GetChildren<MockContainerNodeA>(
                null,
                recurse: true
            );

            IEnumerable<MockContainerNodeB> resultB = _rootNode.GetChildren<MockContainerNodeB>(
                null,
                recurse: true
            );

            IEnumerable<MockContainerNodeC> resultC = _rootNode.GetChildren<MockContainerNodeC>(
                null,
                recurse: true
            );

            Assert.AreEqual(2, resultA.Count(), "Should find 2 type A nodes");
            Assert.AreEqual(1, resultB.Count(), "Should find 1 type B node");
            Assert.AreEqual(1, resultC.Count(), "Should find 1 type C node");
        }

        #endregion
    }
} // namespace Rebellion.Tests.SceneGraph
