using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

[TestFixture]
public class BaseSceneNodeTests
{
    // Mock implementation of BaseSceneNode for testing purposes
    private class MockSceneNode : BaseSceneNode
    {
        private readonly List<ISceneNode> children = new List<ISceneNode>();

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

    private MockSceneNode rootNode;
    private MockSceneNode childNode1;
    private MockSceneNode childNode2;
    private MockSceneNodeA nodeA;
    private MockSceneNodeB nodeB;

    [SetUp]
    public void Setup()
    {
        rootNode = new MockSceneNode
        {
            DisplayName = "RootNode",
            InstanceID = Guid.NewGuid().ToString(),
        };

        childNode1 = new MockSceneNode
        {
            DisplayName = "ChildNode1",
            InstanceID = Guid.NewGuid().ToString(),
        };

        childNode2 = new MockSceneNode
        {
            DisplayName = "ChildNode2",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeA = new MockSceneNodeA
        {
            DisplayName = "NodeA",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeB = new MockSceneNodeB
        {
            DisplayName = "NodeB",
            InstanceID = Guid.NewGuid().ToString(),
        };
    }

    [Test]
    public void SetParent_ValidParent_UpdatesParentReferences()
    {
        rootNode.AddChild(childNode1);
        childNode1.SetParent(rootNode);

        Assert.AreEqual(rootNode, childNode1.GetParent());
        Assert.AreEqual(childNode1, rootNode.GetChildren().First());
    }

    [Test]
    public void GetParentOfType_ValidType_ReturnsCorrectParent()
    {
        childNode1.SetParent(nodeB);
        MockSceneNode result = childNode1.GetParentOfType<MockSceneNodeB>();

        Assert.IsTrue(
            ReferenceEquals(nodeB, result),
            "The parent node returned is not the same instance as expected."
        );
    }

    [Test]
    public void GetParentOfType_CyclicGraphWithDifferentMockTypes_ThrowsInvalidOperationException()
    {
        rootNode.SetParent(childNode1);
        childNode1.SetParent(rootNode);

        Assert.AreEqual(rootNode, childNode1.GetParent(), "NodeB's parent should be NodeA.");
        Assert.AreEqual(childNode1, rootNode.GetParent(), "NodeA's parent should be NodeB.");

        Assert.Throws<InvalidOperationException>(
            () => childNode1.GetParentOfType<MockSceneNodeA>(),
            "Cycle detection did not throw an exception as expected."
        );
    }

    [Test]
    public void SetOwnerInstanceID_AllowedID_SetsSuccessfully()
    {
        childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

        Assert.DoesNotThrow(() => childNode1.SetOwnerInstanceID("Owner1"));
        Assert.AreEqual("Owner1", childNode1.OwnerInstanceID);
    }

    [Test]
    public void SetOwnerInstanceID_DisallowedID_ThrowsInvalidOperationException()
    {
        childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            childNode1.SetOwnerInstanceID("InvalidOwner")
        );
        Assert.That(ex.Message, Does.Contain("Invalid OwnerInstanceID"));
    }

    [Test]
    public void GetChildren_WithPredicateAndType_ReturnsMatchingChildren()
    {
        childNode1.OwnerInstanceID = "Owner1";
        childNode2.OwnerInstanceID = "Owner2";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        IEnumerable<MockSceneNode> matchingChildren = rootNode.GetChildren<MockSceneNode>(
            child => child.OwnerInstanceID == "Owner1",
            false
        );

        Assert.AreEqual(1, matchingChildren.Count());
        Assert.AreEqual(childNode1, matchingChildren.First());
    }

    [Test]
    public void Traverse_HierarchicalNodes_VisitsAllNodes()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(3, visitedNodes.Count); // rootNode + childNode1 + childNode2
    }

    [Test]
    public void SetParent_ChangesParent_UpdatesLastParent()
    {
        childNode1.SetParent(rootNode);
        childNode1.SetParent(null);

        Assert.AreEqual(rootNode, childNode1.GetLastParent());
        Assert.IsNull(childNode1.GetParent());
    }

    [Test]
    public void GetOwnerInstanceID_WhenSet_ReturnsCorrectValue()
    {
        string testOwnerId = "TestOwner123";
        childNode1.OwnerInstanceID = testOwnerId;

        string result = childNode1.GetOwnerInstanceID();

        Assert.AreEqual(testOwnerId, result);
    }

    [Test]
    public void GetOwnerInstanceID_WhenNotSet_ReturnsNull()
    {
        string result = childNode1.GetOwnerInstanceID();

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

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(grandchild1);
        childNode2.AddChild(grandchild2);

        IEnumerable<MockSceneNode> allDescendants = rootNode.GetChildren<MockSceneNode>(null, true);

        Assert.AreEqual(4, allDescendants.Count());
        Assert.IsTrue(allDescendants.Contains(childNode1));
        Assert.IsTrue(allDescendants.Contains(childNode2));
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

        childNode1.OwnerInstanceID = "Owner1";
        childNode2.OwnerInstanceID = "Owner2";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        childNode1.AddChild(grandchild1);
        childNode2.AddChild(grandchild2);

        IEnumerable<MockSceneNode> matchingDescendants = rootNode.GetChildren<MockSceneNode>(
            child => child.OwnerInstanceID == "Owner1",
            true
        );

        Assert.AreEqual(2, matchingDescendants.Count());
        Assert.IsTrue(matchingDescendants.Contains(childNode1));
        Assert.IsTrue(matchingDescendants.Contains(grandchild1));
    }

    [Test]
    public void GetChildren_NonGeneric_ReturnsAllDirectChildren()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);
        rootNode.AddChild(nodeA);

        IEnumerable<ISceneNode> children = rootNode.GetChildren();

        Assert.AreEqual(3, children.Count());
        Assert.IsTrue(children.Contains(childNode1));
        Assert.IsTrue(children.Contains(childNode2));
        Assert.IsTrue(children.Contains(nodeA));
    }

    [Test]
    public void GetChildren_NonGeneric_WhenNoChildren_ReturnsEmptyCollection()
    {
        IEnumerable<ISceneNode> children = rootNode.GetChildren();

        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void SetOwnerInstanceID_NullAllowedOwnerInstanceIDs_AcceptsAnyValue()
    {
        childNode1.AllowedOwnerInstanceIDs = null;

        Assert.DoesNotThrow(() => childNode1.SetOwnerInstanceID("AnyOwner"));
        Assert.AreEqual("AnyOwner", childNode1.OwnerInstanceID);
    }

    [Test]
    public void SetOwnerInstanceID_EmptyAllowedOwnerInstanceIDs_AcceptsAnyValue()
    {
        childNode1.AllowedOwnerInstanceIDs = new List<string>();

        Assert.DoesNotThrow(() => childNode1.SetOwnerInstanceID("AnyOwner"));
        Assert.AreEqual("AnyOwner", childNode1.OwnerInstanceID);
    }

    [Test]
    public void SetOwnerInstanceID_NullValueWithAllowedList_SetsSuccessfully()
    {
        childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

        Assert.DoesNotThrow(() => childNode1.SetOwnerInstanceID(null));
        Assert.IsNull(childNode1.OwnerInstanceID);
    }

    [Test]
    public void SetParent_SameParentTwice_DoesNotChangePrevious()
    {
        rootNode.AddChild(childNode1);
        childNode1.SetParent(rootNode);

        ISceneNode lastParentBefore = childNode1.GetLastParent();
        string lastParentInstanceIDBefore = childNode1.LastParentInstanceID;

        childNode1.SetParent(rootNode);

        Assert.AreEqual(rootNode, childNode1.GetParent());
        Assert.AreEqual(lastParentBefore, childNode1.GetLastParent());
        Assert.AreEqual(lastParentInstanceIDBefore, childNode1.LastParentInstanceID);
    }

    [Test]
    public void ParentInstanceID_WhenParentSet_MatchesParentInstanceID()
    {
        childNode1.SetParent(rootNode);

        Assert.AreEqual(rootNode.InstanceID, childNode1.ParentInstanceID);
    }

    [Test]
    public void ParentInstanceID_WhenParentNull_ReturnsNull()
    {
        childNode1.SetParent(null);

        Assert.IsNull(childNode1.ParentInstanceID);
    }

    [Test]
    public void LastParentInstanceID_AfterParentChange_MatchesPreviousParentInstanceID()
    {
        string originalRootID = rootNode.InstanceID;

        childNode1.SetParent(rootNode);
        childNode1.SetParent(childNode2);

        Assert.AreEqual(originalRootID, childNode1.LastParentInstanceID);
        Assert.AreEqual(childNode2.InstanceID, childNode1.ParentInstanceID);
    }

    [Test]
    public void LastParentInstanceID_WhenParentSetToNull_MatchesPreviousParentInstanceID()
    {
        string originalRootID = rootNode.InstanceID;

        childNode1.SetParent(rootNode);
        childNode1.SetParent(null);

        Assert.AreEqual(originalRootID, childNode1.LastParentInstanceID);
        Assert.IsNull(childNode1.ParentInstanceID);
    }
}
