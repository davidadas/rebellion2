// using System;
// using System.Collections.Generic;
// using System.Linq;
// using NUnit.Framework;

// [TestFixture]
// public class ISceneNodeTests
// {
//     private class TestISceneNode : ISceneNode
//     {
//         private List<ISceneNode> children = new List<ISceneNode>();

//         public override void AddChild(ISceneNode child)
//         {
//             children.Add(child);
//         }

//         public override void RemoveChild(ISceneNode child)
//         {
//             children.Remove(child);
//         }

//         public override IEnumerable<ISceneNode> GetChildren()
//         {
//             return children;
//         }
//     }

//     private class SpecializedTestNode : TestISceneNode { }

//     [Test]
//     public void SetParent_UpdatesParentAndInstanceID()
//     {
//         TestISceneNode parent = new TestISceneNode { InstanceID = "ParentType" };
//         TestISceneNode child = new TestISceneNode();

//         child.SetParent(parent);

//         Assert.AreEqual(parent, child.GetParent());
//         Assert.AreEqual("ParentType", child.ParentInstanceID);
//     }

//     [Test]
//     public void SetParent_UpdatesLastParent()
//     {
//         TestISceneNode parent1 = new TestISceneNode();
//         TestISceneNode parent2 = new TestISceneNode();
//         TestISceneNode child = new TestISceneNode();

//         child.SetParent(parent1);
//         child.SetParent(parent2);

//         Assert.AreEqual(parent2, child.GetParent());
//         Assert.AreEqual(parent1, child.GetLastParent());
//     }

//     [Test]
//     public void GetParentOfType_ReturnsClosestMatchingParent()
//     {
//         SpecializedTestNode root = new SpecializedTestNode();
//         TestISceneNode middle = new TestISceneNode();
//         TestISceneNode leaf = new TestISceneNode();

//         root.AddChild(middle);
//         middle.AddChild(leaf);

//         middle.SetParent(root);
//         leaf.SetParent(middle);

//         Assert.AreEqual(root, leaf.GetParentOfType<SpecializedTestNode>());
//     }

//     [Test]
//     public void GetParentOfType_ReturnsNullWhenNoMatch()
//     {
//         TestISceneNode root = new TestISceneNode();
//         TestISceneNode leaf = new TestISceneNode();

//         root.AddChild(leaf);
//         leaf.SetParent(root);

//         Assert.IsNull(leaf.GetParentOfType<SpecializedTestNode>());
//     }

//     [Test]
//     public void GetParentOfType_ReturnsSelfIfMatching()
//     {
//         SpecializedTestNode node = new SpecializedTestNode();

//         Assert.AreEqual(node, node.GetParentOfType<SpecializedTestNode>());
//     }

//     [Test]
//     public void GetChildrenByOwnerInstanceID_ReturnsMatchingChildren()
//     {
//         TestISceneNode root = new TestISceneNode();
//         TestISceneNode child1 = new TestISceneNode { OwnerInstanceID = "Type1" };
//         TestISceneNode child2 = new TestISceneNode { OwnerInstanceID = "Type2" };
//         TestISceneNode grandchild = new TestISceneNode { OwnerInstanceID = "Type1" };

//         root.AddChild(child1);
//         root.AddChild(child2);
//         child1.AddChild(grandchild);

//         List<TestISceneNode> result = root.GetChildrenByOwnerInstanceID<TestISceneNode>("Type1").ToList();

//         Assert.AreEqual(2, result.Count);
//         Assert.IsTrue(result.Contains(child1));
//         Assert.IsTrue(result.Contains(grandchild));
//     }

//     [Test]
//     public void GetChildrenByOwnerInstanceID_ReturnsEmptyWhenNoMatch()
//     {
//         TestISceneNode root = new TestISceneNode();
//         TestISceneNode child = new TestISceneNode { OwnerInstanceID = "Type1" };

//         root.AddChild(child);

//         IEnumerable<SpecializedTestNode> result =
//             root.GetChildrenByOwnerInstanceID<SpecializedTestNode>("Type1");

//         Assert.IsEmpty(result);
//     }

//     [Test]
//     public void Traverse_VisitsAllNodes()
//     {
//         TestISceneNode root = new TestISceneNode();
//         TestISceneNode child1 = new TestISceneNode();
//         TestISceneNode child2 = new TestISceneNode();
//         TestISceneNode grandchild = new TestISceneNode();

//         root.AddChild(child1);
//         root.AddChild(child2);
//         child1.AddChild(grandchild);

//         List<ISceneNode> visitedNodes = new List<ISceneNode>();
//         root.Traverse(node => visitedNodes.Add(node));

//         Assert.AreEqual(4, visitedNodes.Count);
//         Assert.IsTrue(visitedNodes.Contains(root));
//         Assert.IsTrue(visitedNodes.Contains(child1));
//         Assert.IsTrue(visitedNodes.Contains(child2));
//         Assert.IsTrue(visitedNodes.Contains(grandchild));
//     }

//     [Test]
//     public void AddChild_AddsChildToCollection()
//     {
//         TestISceneNode parent = new TestISceneNode();
//         TestISceneNode child = new TestISceneNode();

//         parent.AddChild(child);

//         Assert.IsTrue(parent.GetChildren().Contains(child));
//     }

//     [Test]
//     public void RemoveChild_RemovesChildFromCollection()
//     {
//         TestISceneNode parent = new TestISceneNode();
//         TestISceneNode child = new TestISceneNode();

//         parent.AddChild(child);
//         parent.RemoveChild(child);

//         Assert.IsFalse(parent.GetChildren().Contains(child));
//     }

//     [Test]
//     public void GetChildren_ReturnsAllAddedChildren()
//     {
//         TestISceneNode parent = new TestISceneNode();
//         TestISceneNode child1 = new TestISceneNode();
//         TestISceneNode child2 = new TestISceneNode();

//         parent.AddChild(child1);
//         parent.AddChild(child2);

//         List<ISceneNode> children = parent.GetChildren().ToList();

//         Assert.AreEqual(2, children.Count);
//         Assert.IsTrue(children.Contains(child1));
//         Assert.IsTrue(children.Contains(child2));
//     }
// }
