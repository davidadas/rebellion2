using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.SceneGraph
{
    [TestFixture]
    public class LeafNodeTests
    {
        // Mock implementation of LeafNode for testing purposes
        private class MockLeafNode : LeafNode
        {
            public MockLeafNode() { }
        }

        // Another mock implementation for type-specific tests
        private class MockLeafNodeA : LeafNode
        {
            public MockLeafNodeA() { }
        }

        // Mock container node to test parent relationships
        private class MockContainerNode : BaseSceneNode
        {
            private readonly List<ISceneNode> children = new List<ISceneNode>();

            public override bool CanAcceptChild(ISceneNode child) => true;

            public override void AddChild(ISceneNode child)
            {
                children.Add(child);
            }

            public override void RemoveChild(ISceneNode child)
            {
                children.Remove(child);
            }

            public override IEnumerable<T> GetChildren<T>(Func<T, bool> predicate, bool recursive)
            {
                IEnumerable<T> direct = children.OfType<T>();

                if (predicate != null)
                {
                    direct = direct.Where(predicate);
                }

                if (!recursive)
                {
                    return direct;
                }

                List<T> result = new List<T>(direct);

                foreach (ISceneNode child in children)
                {
                    result.AddRange(child.GetChildren<T>(predicate, true));
                }

                return result;
            }

            public override IEnumerable<ISceneNode> GetChildren()
            {
                return children;
            }

            public override void Traverse(Action<ISceneNode> action)
            {
                action(this);
                foreach (ISceneNode child in children)
                {
                    child.Traverse(action);
                }
            }
        }

        private MockLeafNode _leafNode;
        private MockLeafNodeA _leafNodeA;
        private MockContainerNode _containerNode;

        [SetUp]
        public void Setup()
        {
            _leafNode = new MockLeafNode
            {
                DisplayName = "LeafNode",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _leafNodeA = new MockLeafNodeA
            {
                DisplayName = "LeafNodeA",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _containerNode = new MockContainerNode
            {
                DisplayName = "ContainerNode",
                InstanceID = Guid.NewGuid().ToString(),
            };
        }

        [Test]
        public void AddChild_WithValidChild_DoesNothing()
        {
            MockLeafNode childNode = new MockLeafNode
            {
                DisplayName = "ChildNode",
                InstanceID = Guid.NewGuid().ToString(),
            };

            // Should not throw and should silently accept
            Assert.DoesNotThrow(() => _leafNode.AddChild(childNode));

            // Verify no children were actually added
            IEnumerable<ISceneNode> children = _leafNode.GetChildren();
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void AddChild_WithNullChild_DoesNothing()
        {
            // Should not throw even with null
            Assert.DoesNotThrow(() => _leafNode.AddChild(null));

            // Verify no children were actually added
            IEnumerable<ISceneNode> children = _leafNode.GetChildren();
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void RemoveChild_WithValidChild_DoesNothing()
        {
            MockLeafNode childNode = new MockLeafNode
            {
                DisplayName = "ChildNode",
                InstanceID = Guid.NewGuid().ToString(),
            };

            // Should not throw and should silently accept
            Assert.DoesNotThrow(() => _leafNode.RemoveChild(childNode));

            // Verify still no children
            IEnumerable<ISceneNode> children = _leafNode.GetChildren();
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void RemoveChild_WithNullChild_DoesNothing()
        {
            // Should not throw even with null
            Assert.DoesNotThrow(() => _leafNode.RemoveChild(null));

            // Verify still no children
            IEnumerable<ISceneNode> children = _leafNode.GetChildren();
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void GetChildren_WithNoChildren_ReturnsEmptyEnumerable()
        {
            IEnumerable<ISceneNode> children = _leafNode.GetChildren();

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void GetChildren_MultipleCallsToGetChildren_ReturnsEmptyEnumerable()
        {
            IEnumerable<ISceneNode> children1 = _leafNode.GetChildren();
            IEnumerable<ISceneNode> children2 = _leafNode.GetChildren();

            Assert.IsNotNull(children1);
            Assert.IsNotNull(children2);
            Assert.AreEqual(0, children1.Count());
            Assert.AreEqual(0, children2.Count());
        }

        [Test]
        public void GetChildrenGeneric_WithTypeFilter_ReturnsEmptyEnumerable()
        {
            IEnumerable<MockLeafNode> children = _leafNode.GetChildren<MockLeafNode>(null, true);

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void GetChildrenGeneric_WithPredicate_ReturnsEmptyEnumerable()
        {
            IEnumerable<MockLeafNode> children = _leafNode.GetChildren<MockLeafNode>(
                node => node.DisplayName == "Test",
                true
            );

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void GetChildrenGeneric_WithNonRecursive_ReturnsEmptyEnumerable()
        {
            IEnumerable<MockLeafNode> children = _leafNode.GetChildren<MockLeafNode>(null, false);

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void GetChildrenGeneric_WithDifferentType_ReturnsEmptyEnumerable()
        {
            IEnumerable<MockLeafNodeA> children = _leafNode.GetChildren<MockLeafNodeA>(null, true);

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void Traverse_WithAction_CallsActionOnSelfOnly()
        {
            List<ISceneNode> visitedNodes = new List<ISceneNode>();

            _leafNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(1, visitedNodes.Count);
            Assert.AreSame(_leafNode, visitedNodes[0]);
        }

        [Test]
        public void Traverse_WithMultipleCalls_CallsActionOnSelfOnlyEachTime()
        {
            int callCount = 0;

            _leafNode.Traverse(_ => callCount++);
            _leafNode.Traverse(_ => callCount++);

            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void Traverse_WithActionThatModifiesState_OnlyAffectsSelf()
        {
            string originalName = _leafNode.DisplayName;
            string newName = "ModifiedName";

            _leafNode.Traverse(node => node.DisplayName = newName);

            Assert.AreEqual(newName, _leafNode.DisplayName);
            Assert.AreNotEqual(originalName, _leafNode.DisplayName);
        }

        [Test]
        public void Traverse_DoesNotTraverseChildren_EvenAfterAddChildCall()
        {
            MockLeafNode childNode = new MockLeafNode
            {
                DisplayName = "ChildNode",
                InstanceID = Guid.NewGuid().ToString(),
            };

            // Try to add a child (which does nothing)
            _leafNode.AddChild(childNode);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _leafNode.Traverse(node => visitedNodes.Add(node));

            // Should only visit the leaf node itself
            Assert.AreEqual(1, visitedNodes.Count);
            Assert.AreSame(_leafNode, visitedNodes[0]);
        }

        [Test]
        public void SetParent_WithValidParent_UpdatesParentReferences()
        {
            _leafNode.SetParent(_containerNode);

            Assert.AreSame(_containerNode, _leafNode.GetParent());
            Assert.AreEqual(_containerNode.InstanceID, _leafNode.ParentInstanceID);
        }

        [Test]
        public void SetParent_WithNull_ClearsParentReferences()
        {
            _leafNode.SetParent(_containerNode);
            _leafNode.SetParent(null);

            Assert.IsNull(_leafNode.GetParent());
            Assert.IsNull(_leafNode.ParentInstanceID);
        }

        [Test]
        public void SetParent_ChangingParent_UpdatesLastParent()
        {
            MockContainerNode firstParent = new MockContainerNode
            {
                DisplayName = "FirstParent",
                InstanceID = Guid.NewGuid().ToString(),
            };

            MockContainerNode secondParent = new MockContainerNode
            {
                DisplayName = "SecondParent",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _leafNode.SetParent(firstParent);
            _leafNode.SetParent(secondParent);

            Assert.AreSame(secondParent, _leafNode.GetParent());
            Assert.AreSame(firstParent, _leafNode.GetLastParent());
            Assert.AreEqual(secondParent.InstanceID, _leafNode.ParentInstanceID);
            Assert.AreEqual(firstParent.InstanceID, _leafNode.LastParentInstanceID);
        }

        [Test]
        public void SetParent_WithSameParent_DoesNothing()
        {
            _leafNode.SetParent(_containerNode);
            ISceneNode lastParentBefore = _leafNode.GetLastParent();

            _leafNode.SetParent(_containerNode);

            Assert.AreSame(_containerNode, _leafNode.GetParent());
            Assert.AreSame(lastParentBefore, _leafNode.GetLastParent());
        }

        [Test]
        public void SetParent_RemovesFromOldParent_WhenChangingParent()
        {
            MockContainerNode oldParent = new MockContainerNode
            {
                DisplayName = "OldParent",
                InstanceID = Guid.NewGuid().ToString(),
            };

            MockContainerNode newParent = new MockContainerNode
            {
                DisplayName = "NewParent",
                InstanceID = Guid.NewGuid().ToString(),
            };

            oldParent.AddChild(_leafNode);
            _leafNode.SetParent(oldParent);

            Assert.AreEqual(1, oldParent.GetChildren().Count());

            _leafNode.SetParent(newParent);

            Assert.AreEqual(0, oldParent.GetChildren().Count());
            Assert.AreSame(newParent, _leafNode.GetParent());
        }

        [Test]
        public void SetParent_WithLeafNodeAsParent_WorksCorrectly()
        {
            // A leaf node can be a parent, even though it can't have children
            MockLeafNode parentLeaf = new MockLeafNode
            {
                DisplayName = "ParentLeaf",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _leafNode.SetParent(parentLeaf);

            Assert.AreSame(parentLeaf, _leafNode.GetParent());
            Assert.AreEqual(parentLeaf.InstanceID, _leafNode.ParentInstanceID);
        }

        [Test]
        public void LeafNode_InheritedProperties_WorkCorrectly()
        {
            string displayName = "TestLeafNode";

            _leafNode.DisplayName = displayName;

            Assert.AreEqual(displayName, _leafNode.DisplayName);
            Assert.IsNotNull(
                _leafNode.InstanceID,
                "InstanceID should be auto-generated and not null"
            );
        }

        [Test]
        public void GetChildren_AfterMultipleAddChildCalls_RemainsEmpty()
        {
            MockLeafNode child1 = new MockLeafNode
            {
                DisplayName = "Child1",
                InstanceID = Guid.NewGuid().ToString(),
            };

            MockLeafNode child2 = new MockLeafNode
            {
                DisplayName = "Child2",
                InstanceID = Guid.NewGuid().ToString(),
            };

            MockLeafNode child3 = new MockLeafNode
            {
                DisplayName = "Child3",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _leafNode.AddChild(child1);
            _leafNode.AddChild(child2);
            _leafNode.AddChild(child3);

            IEnumerable<ISceneNode> children = _leafNode.GetChildren();
            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void GetChildrenGeneric_WithComplexPredicate_ReturnsEmpty()
        {
            IEnumerable<MockLeafNode> children = _leafNode.GetChildren<MockLeafNode>(
                node => node.DisplayName.StartsWith("Test") && node.InstanceID != null,
                true
            );

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count());
        }
    }
} // namespace Rebellion.Tests.SceneGraph
