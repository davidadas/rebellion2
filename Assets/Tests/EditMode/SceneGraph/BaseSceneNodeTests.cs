using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.SceneGraph
{
    [TestFixture]
    public class BaseSceneNodeTests
    {
        // Mock implementation of BaseSceneNode for testing purposes
        private class MockSceneNode : BaseSceneNode
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

        private class MockSceneNodeA : MockSceneNode { }

        private class MockSceneNodeB : MockSceneNode { }

        private MockSceneNode _rootNode;
        private MockSceneNode _childNode1;
        private MockSceneNode _childNode2;
        private MockSceneNodeA _nodeA;
        private MockSceneNodeB _nodeB;

        [SetUp]
        public void Setup()
        {
            _rootNode = new MockSceneNode
            {
                DisplayName = "RootNode",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _childNode1 = new MockSceneNode
            {
                DisplayName = "ChildNode1",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _childNode2 = new MockSceneNode
            {
                DisplayName = "ChildNode2",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeA = new MockSceneNodeA
            {
                DisplayName = "NodeA",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _nodeB = new MockSceneNodeB
            {
                DisplayName = "NodeB",
                InstanceID = Guid.NewGuid().ToString(),
            };
        }

        [Test]
        public void SetParent_ValidParent_UpdatesParentReferences()
        {
            _rootNode.AddChild(_childNode1);
            _childNode1.SetParent(_rootNode);

            Assert.AreEqual(_rootNode, _childNode1.GetParent());
            Assert.AreEqual(_childNode1, _rootNode.GetChildren().First());
        }

        [Test]
        public void GetParentOfType_ValidType_ReturnsCorrectParent()
        {
            _childNode1.SetParent(_nodeB);
            MockSceneNode result = _childNode1.GetParentOfType<MockSceneNodeB>();

            Assert.IsTrue(
                ReferenceEquals(_nodeB, result),
                "The parent node returned is not the same instance as expected."
            );
        }

        [Test]
        public void GetParentOfType_CyclicGraphWithDifferentMockTypes_ThrowsInvalidOperationException()
        {
            _rootNode.SetParent(_childNode1);
            _childNode1.SetParent(_rootNode);

            Assert.AreEqual(_rootNode, _childNode1.GetParent(), "NodeB's parent should be NodeA.");
            Assert.AreEqual(_childNode1, _rootNode.GetParent(), "NodeA's parent should be NodeB.");

            Assert.Throws<InvalidOperationException>(
                () => _childNode1.GetParentOfType<MockSceneNodeA>(),
                "Cycle detection did not throw an exception as expected."
            );
        }

        [Test]
        public void SetOwnerInstanceID_AllowedID_SetsSuccessfully()
        {
            _childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

            Assert.DoesNotThrow(() => _childNode1.SetOwnerInstanceID("Owner1"));
            Assert.AreEqual("Owner1", _childNode1.OwnerInstanceID);
        }

        [Test]
        public void SetOwnerInstanceID_DisallowedID_ThrowsInvalidOperationException()
        {
            _childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                _childNode1.SetOwnerInstanceID("InvalidOwner")
            );
            Assert.That(ex.Message, Does.Contain("Invalid OwnerInstanceID"));
        }

        [Test]
        public void GetChildren_WithPredicateAndType_ReturnsMatchingChildren()
        {
            _childNode1.OwnerInstanceID = "Owner1";
            _childNode2.OwnerInstanceID = "Owner2";

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            IEnumerable<MockSceneNode> matchingChildren = _rootNode.GetChildren<MockSceneNode>(
                child => child.OwnerInstanceID == "Owner1",
                false
            );

            Assert.AreEqual(1, matchingChildren.Count());
            Assert.AreEqual(_childNode1, matchingChildren.First());
        }

        [Test]
        public void Traverse_HierarchicalNodes_VisitsAllNodes()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);

            List<ISceneNode> visitedNodes = new List<ISceneNode>();
            _rootNode.Traverse(node => visitedNodes.Add(node));

            Assert.AreEqual(3, visitedNodes.Count); // rootNode + childNode1 + childNode2
        }

        [Test]
        public void SetParent_ChangesParent_UpdatesLastParent()
        {
            _childNode1.SetParent(_rootNode);
            _childNode1.SetParent(null);

            Assert.AreEqual(_rootNode, _childNode1.GetLastParent());
            Assert.IsNull(_childNode1.GetParent());
        }

        [Test]
        public void GetOwnerInstanceID_WhenSet_ReturnsCorrectValue()
        {
            string testOwnerId = "TestOwner123";
            _childNode1.OwnerInstanceID = testOwnerId;

            string result = _childNode1.GetOwnerInstanceID();

            Assert.AreEqual(testOwnerId, result);
        }

        [Test]
        public void GetOwnerInstanceID_WhenNotSet_ReturnsNull()
        {
            string result = _childNode1.GetOwnerInstanceID();

            Assert.IsNull(result);
        }

        [Test]
        public void GetChildren_RecursiveTraversal_ReturnsAllDescendants()
        {
            MockSceneNode grandchild1 = new MockSceneNode
            {
                DisplayName = "Grandchild1",
                InstanceID = Guid.NewGuid().ToString(),
            };

            MockSceneNode grandchild2 = new MockSceneNode
            {
                DisplayName = "Grandchild2",
                InstanceID = Guid.NewGuid().ToString(),
            };

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(grandchild1);
            _childNode2.AddChild(grandchild2);

            IEnumerable<MockSceneNode> allDescendants = _rootNode.GetChildren<MockSceneNode>(
                null,
                true
            );

            Assert.AreEqual(4, allDescendants.Count());
            Assert.IsTrue(allDescendants.Contains(_childNode1));
            Assert.IsTrue(allDescendants.Contains(_childNode2));
            Assert.IsTrue(allDescendants.Contains(grandchild1));
            Assert.IsTrue(allDescendants.Contains(grandchild2));
        }

        [Test]
        public void GetChildren_RecursiveWithPredicate_ReturnsMatchingDescendants()
        {
            MockSceneNode grandchild1 = new MockSceneNode
            {
                DisplayName = "Grandchild1",
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = "Owner1",
            };

            MockSceneNode grandchild2 = new MockSceneNode
            {
                DisplayName = "Grandchild2",
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = "Owner2",
            };

            _childNode1.OwnerInstanceID = "Owner1";
            _childNode2.OwnerInstanceID = "Owner2";

            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _childNode1.AddChild(grandchild1);
            _childNode2.AddChild(grandchild2);

            IEnumerable<MockSceneNode> matchingDescendants = _rootNode.GetChildren<MockSceneNode>(
                child => child.OwnerInstanceID == "Owner1",
                true
            );

            Assert.AreEqual(2, matchingDescendants.Count());
            Assert.IsTrue(matchingDescendants.Contains(_childNode1));
            Assert.IsTrue(matchingDescendants.Contains(grandchild1));
        }

        [Test]
        public void GetChildren_NonGeneric_ReturnsAllDirectChildren()
        {
            _rootNode.AddChild(_childNode1);
            _rootNode.AddChild(_childNode2);
            _rootNode.AddChild(_nodeA);

            IEnumerable<ISceneNode> children = _rootNode.GetChildren();

            Assert.AreEqual(3, children.Count());
            Assert.IsTrue(children.Contains(_childNode1));
            Assert.IsTrue(children.Contains(_childNode2));
            Assert.IsTrue(children.Contains(_nodeA));
        }

        [Test]
        public void GetChildren_NonGeneric_WhenNoChildren_ReturnsEmptyCollection()
        {
            IEnumerable<ISceneNode> children = _rootNode.GetChildren();

            Assert.AreEqual(0, children.Count());
        }

        [Test]
        public void SetOwnerInstanceID_NullAllowedOwnerInstanceIDs_AcceptsAnyValue()
        {
            _childNode1.AllowedOwnerInstanceIDs = null;

            Assert.DoesNotThrow(() => _childNode1.SetOwnerInstanceID("AnyOwner"));
            Assert.AreEqual("AnyOwner", _childNode1.OwnerInstanceID);
        }

        [Test]
        public void SetOwnerInstanceID_EmptyAllowedOwnerInstanceIDs_AcceptsAnyValue()
        {
            _childNode1.AllowedOwnerInstanceIDs = new List<string>();

            Assert.DoesNotThrow(() => _childNode1.SetOwnerInstanceID("AnyOwner"));
            Assert.AreEqual("AnyOwner", _childNode1.OwnerInstanceID);
        }

        [Test]
        public void SetOwnerInstanceID_NullValueWithAllowedList_SetsSuccessfully()
        {
            _childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

            Assert.DoesNotThrow(() => _childNode1.SetOwnerInstanceID(null));
            Assert.IsNull(_childNode1.OwnerInstanceID);
        }

        [Test]
        public void SetParent_SameParentTwice_DoesNotChangePrevious()
        {
            _rootNode.AddChild(_childNode1);
            _childNode1.SetParent(_rootNode);

            ISceneNode lastParentBefore = _childNode1.GetLastParent();
            string lastParentInstanceIDBefore = _childNode1.LastParentInstanceID;

            _childNode1.SetParent(_rootNode);

            Assert.AreEqual(_rootNode, _childNode1.GetParent());
            Assert.AreEqual(lastParentBefore, _childNode1.GetLastParent());
            Assert.AreEqual(lastParentInstanceIDBefore, _childNode1.LastParentInstanceID);
        }

        [Test]
        public void ParentInstanceID_WhenParentSet_MatchesParentInstanceID()
        {
            _childNode1.SetParent(_rootNode);

            Assert.AreEqual(_rootNode.InstanceID, _childNode1.ParentInstanceID);
        }

        [Test]
        public void ParentInstanceID_WhenParentNull_ReturnsNull()
        {
            _childNode1.SetParent(null);

            Assert.IsNull(_childNode1.ParentInstanceID);
        }

        [Test]
        public void LastParentInstanceID_AfterParentChange_MatchesPreviousParentInstanceID()
        {
            string originalRootID = _rootNode.InstanceID;

            _childNode1.SetParent(_rootNode);
            _childNode1.SetParent(_childNode2);

            Assert.AreEqual(originalRootID, _childNode1.LastParentInstanceID);
            Assert.AreEqual(_childNode2.InstanceID, _childNode1.ParentInstanceID);
        }

        [Test]
        public void LastParentInstanceID_WhenParentSetToNull_MatchesPreviousParentInstanceID()
        {
            string originalRootID = _rootNode.InstanceID;

            _childNode1.SetParent(_rootNode);
            _childNode1.SetParent(null);

            Assert.AreEqual(originalRootID, _childNode1.LastParentInstanceID);
            Assert.IsNull(_childNode1.ParentInstanceID);
        }
    }
} // namespace Rebellion.Tests.SceneGraph
