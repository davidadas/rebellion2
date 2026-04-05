using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

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

    private MockLeafNode leafNode;
    private MockLeafNodeA leafNodeA;
    private MockContainerNode containerNode;

    [SetUp]
    public void Setup()
    {
        leafNode = new MockLeafNode
        {
            DisplayName = "LeafNode",
            InstanceID = Guid.NewGuid().ToString(),
        };

        leafNodeA = new MockLeafNodeA
        {
            DisplayName = "LeafNodeA",
            InstanceID = Guid.NewGuid().ToString(),
        };

        containerNode = new MockContainerNode
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
        Assert.DoesNotThrow(() => leafNode.AddChild(childNode));

        // Verify no children were actually added
        IEnumerable<ISceneNode> children = leafNode.GetChildren();
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void AddChild_WithNullChild_DoesNothing()
    {
        // Should not throw even with null
        Assert.DoesNotThrow(() => leafNode.AddChild(null));

        // Verify no children were actually added
        IEnumerable<ISceneNode> children = leafNode.GetChildren();
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
        Assert.DoesNotThrow(() => leafNode.RemoveChild(childNode));

        // Verify still no children
        IEnumerable<ISceneNode> children = leafNode.GetChildren();
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void RemoveChild_WithNullChild_DoesNothing()
    {
        // Should not throw even with null
        Assert.DoesNotThrow(() => leafNode.RemoveChild(null));

        // Verify still no children
        IEnumerable<ISceneNode> children = leafNode.GetChildren();
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void GetChildren_WithNoChildren_ReturnsEmptyEnumerable()
    {
        IEnumerable<ISceneNode> children = leafNode.GetChildren();

        Assert.IsNotNull(children);
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void GetChildren_MultipleCallsToGetChildren_ReturnsEmptyEnumerable()
    {
        IEnumerable<ISceneNode> children1 = leafNode.GetChildren();
        IEnumerable<ISceneNode> children2 = leafNode.GetChildren();

        Assert.IsNotNull(children1);
        Assert.IsNotNull(children2);
        Assert.AreEqual(0, children1.Count());
        Assert.AreEqual(0, children2.Count());
    }

    [Test]
    public void GetChildrenGeneric_WithTypeFilter_ReturnsEmptyEnumerable()
    {
        IEnumerable<MockLeafNode> children = leafNode.GetChildren<MockLeafNode>(null, true);

        Assert.IsNotNull(children);
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void GetChildrenGeneric_WithPredicate_ReturnsEmptyEnumerable()
    {
        IEnumerable<MockLeafNode> children = leafNode.GetChildren<MockLeafNode>(
            node => node.DisplayName == "Test",
            true
        );

        Assert.IsNotNull(children);
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void GetChildrenGeneric_WithNonRecursive_ReturnsEmptyEnumerable()
    {
        IEnumerable<MockLeafNode> children = leafNode.GetChildren<MockLeafNode>(null, false);

        Assert.IsNotNull(children);
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void GetChildrenGeneric_WithDifferentType_ReturnsEmptyEnumerable()
    {
        IEnumerable<MockLeafNodeA> children = leafNode.GetChildren<MockLeafNodeA>(null, true);

        Assert.IsNotNull(children);
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void Traverse_WithAction_CallsActionOnSelfOnly()
    {
        List<ISceneNode> visitedNodes = new List<ISceneNode>();

        leafNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(1, visitedNodes.Count);
        Assert.AreSame(leafNode, visitedNodes[0]);
    }

    [Test]
    public void Traverse_WithMultipleCalls_CallsActionOnSelfOnlyEachTime()
    {
        int callCount = 0;

        leafNode.Traverse(node => callCount++);
        leafNode.Traverse(node => callCount++);

        Assert.AreEqual(2, callCount);
    }

    [Test]
    public void Traverse_WithActionThatModifiesState_OnlyAffectsSelf()
    {
        string originalName = leafNode.DisplayName;
        string newName = "ModifiedName";

        leafNode.Traverse(node => node.DisplayName = newName);

        Assert.AreEqual(newName, leafNode.DisplayName);
        Assert.AreNotEqual(originalName, leafNode.DisplayName);
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
        leafNode.AddChild(childNode);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        leafNode.Traverse(node => visitedNodes.Add(node));

        // Should only visit the leaf node itself
        Assert.AreEqual(1, visitedNodes.Count);
        Assert.AreSame(leafNode, visitedNodes[0]);
    }

    [Test]
    public void SetParent_WithValidParent_UpdatesParentReferences()
    {
        leafNode.SetParent(containerNode);

        Assert.AreSame(containerNode, leafNode.GetParent());
        Assert.AreEqual(containerNode.InstanceID, leafNode.ParentInstanceID);
    }

    [Test]
    public void SetParent_WithNull_ClearsParentReferences()
    {
        leafNode.SetParent(containerNode);
        leafNode.SetParent(null);

        Assert.IsNull(leafNode.GetParent());
        Assert.IsNull(leafNode.ParentInstanceID);
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

        leafNode.SetParent(firstParent);
        leafNode.SetParent(secondParent);

        Assert.AreSame(secondParent, leafNode.GetParent());
        Assert.AreSame(firstParent, leafNode.GetLastParent());
        Assert.AreEqual(secondParent.InstanceID, leafNode.ParentInstanceID);
        Assert.AreEqual(firstParent.InstanceID, leafNode.LastParentInstanceID);
    }

    [Test]
    public void SetParent_WithSameParent_DoesNothing()
    {
        leafNode.SetParent(containerNode);
        ISceneNode lastParentBefore = leafNode.GetLastParent();

        leafNode.SetParent(containerNode);

        Assert.AreSame(containerNode, leafNode.GetParent());
        Assert.AreSame(lastParentBefore, leafNode.GetLastParent());
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

        oldParent.AddChild(leafNode);
        leafNode.SetParent(oldParent);

        Assert.AreEqual(1, oldParent.GetChildren().Count());

        leafNode.SetParent(newParent);

        Assert.AreEqual(0, oldParent.GetChildren().Count());
        Assert.AreSame(newParent, leafNode.GetParent());
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

        leafNode.SetParent(parentLeaf);

        Assert.AreSame(parentLeaf, leafNode.GetParent());
        Assert.AreEqual(parentLeaf.InstanceID, leafNode.ParentInstanceID);
    }

    [Test]
    public void LeafNode_InheritedProperties_WorkCorrectly()
    {
        string displayName = "TestLeafNode";

        leafNode.DisplayName = displayName;

        Assert.AreEqual(displayName, leafNode.DisplayName);
        Assert.IsNotNull(leafNode.InstanceID, "InstanceID should be auto-generated and not null");
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

        leafNode.AddChild(child1);
        leafNode.AddChild(child2);
        leafNode.AddChild(child3);

        IEnumerable<ISceneNode> children = leafNode.GetChildren();
        Assert.AreEqual(0, children.Count());
    }

    [Test]
    public void GetChildrenGeneric_WithComplexPredicate_ReturnsEmpty()
    {
        IEnumerable<MockLeafNode> children = leafNode.GetChildren<MockLeafNode>(
            node => node.DisplayName.StartsWith("Test") && node.InstanceID != null,
            true
        );

        Assert.IsNotNull(children);
        Assert.AreEqual(0, children.Count());
    }
}
