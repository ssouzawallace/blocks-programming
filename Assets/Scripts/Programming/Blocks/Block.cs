﻿using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public abstract class Block : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {

	public class Connection  {
		const float kMinimumAttachRadius = 20.0f;

		public enum SocketType {SocketTypeMale, SocketTypeFemale};
		public enum ConnectionType {ConnectionTypeRegular, ConnectionTypeLogic, ConnectionTypeNumber};

		private Block 			attachedBlock;
		private Connection 		attachedConnection;

		private Block 			ownerBlock;

		private SocketType 		socketType;
		private ConnectionType 	connectionType;
		private Vector2 		relativePosition;

		public event Action<Connection> attachmentChangedEvent;

		public Connection (Block ownerBlock, SocketType socketType, ConnectionType connectionType, Vector2 relativePosition) {
			this.ownerBlock 		= ownerBlock;

			this.socketType 		= socketType;
			this.connectionType 	= connectionType;
			this.relativePosition 	= relativePosition;
		}

		public void SetRelativePosition(Vector2 relativePosition) {
			this.relativePosition = relativePosition;
		}

		public SocketType GetSocketType() {
			return this.socketType;
		}

		public Block GetAttachedBlock () {
			return this.attachedBlock;
		}
		public void Attach(Block block, Connection connection) {
			if (this.attachedBlock == null) {
				this.attachedBlock = block;
				this.attachedConnection = connection;

				connection.Attach (this.ownerBlock, this);

				if (attachmentChangedEvent != null) {
					attachmentChangedEvent(this);
				}
			}
		}
		public void Detach() {
			if (this.attachedBlock != null) {
				this.attachedBlock = null;

				this.attachedConnection.Detach();
				this.attachedConnection = null;

				if (attachmentChangedEvent != null) {
					attachmentChangedEvent(this);
				}
			}
		}

		public Vector2 AbsolutePosition() {
			float parentScale = this.ownerBlock.transform.parent.GetComponent<RectTransform> ().localScale.x;

			return new Vector2(this.ownerBlock.transform.position.x, this.ownerBlock.transform.position.y) + this.relativePosition*parentScale;
		}
		float DistanceTo(Connection connection) {
			return Vector2.Distance (this.AbsolutePosition(), connection.AbsolutePosition());
		}
		public bool TryAttachWithBlock (Block block) {

			foreach (Connection connection in block.connections) {
				if (this.socketType != connection.socketType
				    &&
				    this.connectionType == connection.connectionType
				    &&
					connection.GetAttachedBlock() == null
				    && 
				    this.GetAttachedBlock() == null				    
				    && !(this.ownerBlock.connections.IndexOf(this) == 0 && block.connections.IndexOf(connection) == 0)
				    &&
					this.DistanceTo (connection) < kMinimumAttachRadius) {

					if (this.ownerBlock.connections[0].Equals(this)) {
						Vector2 delta =  connection.AbsolutePosition() - this.AbsolutePosition();
						
						this.ownerBlock.ApplyDelta(delta);
					}
					else {
						Vector2 delta =  this.AbsolutePosition() - connection.AbsolutePosition();						

						block.ApplyDelta(delta);
					}

					this.Attach(block, connection);

					return true;
				}
			}

			return false;
		}
	}

	[HideInInspector]
	public RectTransform rectTransform;
	[HideInInspector]
	public LayoutElement layoutElement;

	protected Image image;
	protected Shadow[] shadows;
	protected ArrayList connections = new ArrayList();

	public bool leaveClone = false;

	public virtual void Start () {
		this.rectTransform 	= gameObject.GetComponent <RectTransform> ();
		this.layoutElement 	= gameObject.GetComponent <LayoutElement> ();

		this.image 			= gameObject.GetComponent <Image> ();
		this.shadows 			= gameObject.GetComponentsInChildren <Shadow> ();

		foreach (Shadow shadow in this.shadows) {
			shadow.enabled = false;
		}
	}
	public virtual void HierarchyChanged() {
		Connection firstConnection = this.connections [0] as Connection;

		if (firstConnection.GetAttachedBlock () != null) {
			firstConnection.GetAttachedBlock ().HierarchyChanged();
		}
	}

	public void SetShadowActive (bool active) {
		foreach (Shadow shadow in this.shadows) {
			shadow.enabled = active;
		}
	}
	
	public void Detach () {
		Connection firstConnection = this.connections [0] as Connection;
		Block previousBlock = firstConnection.GetAttachedBlock ();
		firstConnection.Detach ();

		if (previousBlock != null) {
			previousBlock.HierarchyChanged ();
		}
	}

	public void ApplyDelta(Vector2 delta) {
		ArrayList descendingBlocks = this.DescendingBlocks ();

		foreach (Block block in descendingBlocks) {
			block.transform.position += new Vector3 (delta.x, delta.y);
		}
	}

	public bool TryAttachInSomeConnectionWithBlock (Block block) {
		if (this.Equals (block)) {
			return false;
		}

		ArrayList descendingBlocks = this.DescendingBlocks ();
		
		foreach (Block aBlock in descendingBlocks) {		
			foreach (Connection conection in aBlock.connections) {
				if (conection.TryAttachWithBlock (block)) {
					Connection firstConnection = this.connections [0] as Connection;
					Block previousBlock = firstConnection.GetAttachedBlock ();

					if (previousBlock != null) {
						previousBlock.HierarchyChanged ();
					}

					return true;
				}
			}
		}
		return false;
	}

	public ArrayList DescendingBlocks () {
		ArrayList arrayList = new ArrayList ();
		arrayList.Add (this);

		for (int i = 1; i < this.connections.Count; ++i) {
			Connection connection = this.connections[i] as Connection;

			if (connection.GetAttachedBlock() != null && connection.GetAttachedBlock().Equals(this) == false) {
				ArrayList descendingBlocks = connection.GetAttachedBlock().DescendingBlocks();
				foreach (Block block in descendingBlocks) {
					arrayList.Add(block);
				}
			}
		}

		return arrayList;
	}

	#region Abstract

	public abstract string GetCode ();

	#endregion

	#region Drag
	
	public void OnBeginDrag (PointerEventData eventData) {
		if (this.leaveClone == true) {
			GameObject go = Instantiate (this.gameObject);

			go.GetComponent<Block> ().leaveClone = true;

			go.transform.SetParent (this.transform.parent, false);
			go.transform.SetSiblingIndex (this.transform.GetSiblingIndex ());

			go.GetComponent<RectTransform> ().anchoredPosition = this.rectTransform.anchoredPosition;
			go.GetComponent<RectTransform> ().sizeDelta = this.rectTransform.sizeDelta;
			go.GetComponent<RectTransform> ().anchorMin = this.rectTransform.anchorMin;
			go.GetComponent<RectTransform> ().anchorMax = this.rectTransform.anchorMax;

			this.leaveClone = false;
		}

		// Desconecta do bloco acima
		this.Detach ();

		Vector3 before = this.transform.position;		
		this.transform.SetParent(GameObject.FindWithTag("Canvas").transform, false);		
		Vector3 after = this.transform.position;		
		this.transform.position += (before-after);

		// Deixa todos os blocos descendetes no topo da telas
		ArrayList descendingBlocks = this.DescendingBlocks ();

		foreach (Block block in descendingBlocks) {
			block.transform.SetSiblingIndex (block.transform.parent.childCount - 1);
			block.SetShadowActive (true);
		}

	}
	
	Vector3 lastMousePosition = Vector3.zero;
	public void OnDrag (PointerEventData eventData) {

		// Aplica delta em função do drag
		if (lastMousePosition == Vector3.zero) {
			lastMousePosition = Input.mousePosition;
		}
		else {
			this.ApplyDelta (Input.mousePosition - lastMousePosition);

			lastMousePosition = Input.mousePosition;
		}
	}

	public void OnEndDrag (PointerEventData eventData) {

		GameObject codeContentGO = GameObject.FindWithTag ("CodeContent");
		if (transform.parent.gameObject.Equals (codeContentGO) == false) {
			RectTransform rect = codeContentGO.GetComponent<RectTransform> ();
			Vector2 mousePos = new Vector2 (Input.mousePosition.x, Input.mousePosition.y);

			if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos)) {
				Vector3 previousPosition = this.transform.position;
				this.transform.SetParent(codeContentGO.transform, false);
				this.transform.position = previousPosition;
			}
			else {
				// Coloca blocos no topo
				ArrayList descending = DescendingBlocks ();
				foreach (Block b in descending) {
					Destroy(b.gameObject);
				}

				return;
			}
		}

		lastMousePosition = Vector3.zero;

		ArrayList descendingBlocks = this.DescendingBlocks ();
		foreach (Block block in descendingBlocks) {
			block.SetShadowActive(false);
		}

		// Tenta conectar com algum bloco
		GameObject[] GOs = GameObject.FindGameObjectsWithTag ("Block");

		foreach (GameObject GO in GOs) {
			Block block = GO.GetComponent<Block>() as Block;

			if (this.TryAttachInSomeConnectionWithBlock (block.GetComponent<Block>())) {
				break;
			}
		}

		Debug.Log (GetCode ());
	}

	#endregion

	void OnDrawGizmos() {
		if (!Application.isPlaying) return;
		
		foreach (Connection connection in this.connections) {
			if (connection.GetSocketType() == Connection.SocketType.SocketTypeMale) {
				Gizmos.color = Color.blue;
			}
			else {
				Gizmos.color = Color.red;
			}
			Gizmos.DrawSphere(connection.AbsolutePosition(), 10);
		}
	}
}
