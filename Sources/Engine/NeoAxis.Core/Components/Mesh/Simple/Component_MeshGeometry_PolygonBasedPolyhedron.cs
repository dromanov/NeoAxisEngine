// Copyright (C) NeoAxis Group Ltd. 8 Copthall, Roseau Valley, 00152 Commonwealth of Dominica.
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using NeoAxis.Editor;

namespace NeoAxis
{
	/// <summary>
	/// Mesh geometry in the form of polyhedron generated by thickening a polygon.
	/// </summary>
	[ObjectCreationMode( typeof( CreationModePolyhedron ) )]
	public class Component_MeshGeometry_PolygonBasedPolyhedron : Component_MeshGeometry_Procedural
	{
		/// <summary>
		/// Whether the points are clockwise.
		/// </summary>
		[DefaultValue( false )]
		public Reference<bool> Clockwise
		{
			get { if( _clockwise.BeginGet() ) Clockwise = _clockwise.Get( this ); return _clockwise.value; }
			set { if( _clockwise.BeginSet( ref value ) ) { try { ClockwiseChanged?.Invoke( this ); ShouldRecompileMesh(); } finally { _clockwise.EndSet(); } } }
		}
		/// <summary>Occurs when the <see cref="Clockwise"/> property value changes.</summary>
		public event Action<Component_MeshGeometry_PolygonBasedPolyhedron> ClockwiseChanged;
		ReferenceField<bool> _clockwise = false;

		/// <summary>
		/// The height of the shape.
		/// </summary>
		[DefaultValue( 0.0 )]
		[Range( 0, 100, RangeAttribute.ConvenientDistributionEnum.Exponential, 4 )]
		public Reference<double> Height
		{
			get { if( _height.BeginGet() ) Height = _height.Get( this ); return _height.value; }
			set { if( _height.BeginSet( ref value ) ) { try { HeightChanged?.Invoke( this ); ShouldRecompileMesh(); } finally { _height.EndSet(); } } }
		}
		/// <summary>Occurs when the <see cref="Height"/> property value changes.</summary>
		public event Action<Component_MeshGeometry_PolygonBasedPolyhedron> HeightChanged;
		ReferenceField<double> _height = 0.0;

		/// <summary>
		/// Whether the box is flipped.
		/// </summary>
		[DefaultValue( false )]
		public Reference<bool> InsideOut
		{
			get { if( _insideOut.BeginGet() ) InsideOut = _insideOut.Get( this ); return _insideOut.value; }
			set { if( _insideOut.BeginSet( ref value ) ) { try { InsideOutChanged?.Invoke( this ); ShouldRecompileMesh(); } finally { _insideOut.EndSet(); } } }
		}
		/// <summary>Occurs when the <see cref="InsideOut"/> property value changes.</summary>
		public event Action<Component_MeshGeometry_PolygonBasedPolyhedron> InsideOutChanged;
		ReferenceField<bool> _insideOut = false;

		/// <summary>
		/// Whether to always display point labels or only when the object in the scene is selected.
		/// </summary>
		[DefaultValue( false )]
		public Reference<bool> AlwaysDisplayPointLabels
		{
			get { if( _alwaysDisplayPointLabels.BeginGet() ) AlwaysDisplayPointLabels = _alwaysDisplayPointLabels.Get( this ); return _alwaysDisplayPointLabels.value; }
			set { if( _alwaysDisplayPointLabels.BeginSet( ref value ) ) { try { AlwaysDisplayPointLabelsChanged?.Invoke( this ); } finally { _alwaysDisplayPointLabels.EndSet(); } } }
		}
		/// <summary>Occurs when the <see cref="AlwaysDisplayPointLabels"/> property value changes.</summary>
		public event Action<Component_MeshGeometry_PolygonBasedPolyhedron> AlwaysDisplayPointLabelsChanged;
		ReferenceField<bool> _alwaysDisplayPointLabels = false;

		/////////////////////////////////////////

		/// <summary>
		/// A class for providing the creation of a <see cref="Component_MeshGeometry_PolygonBasedPolyhedron"/> in the editor.
		/// </summary>
		public class CreationModePolyhedron : ObjectCreationMode
		{
			Rectangle? lastStartPointRectangle;

			bool heightStage;
			Vector3 heightStagePosition;

			//

			public CreationModePolyhedron( DocumentWindowWithViewport documentWindow, Component creatingObject )
				: base( documentWindow, creatingObject )
			{
				var position = CreatingObject.TransformV.Position;
				if( CalculatePointPosition( documentWindow.Viewport, out var position2, out _ ) )
					position = position2;

				var point = MeshGeometry.CreateComponent<Component_MeshGeometry_PolygonBasedPolyhedron_Point>( enabled: false );
				point.Name = MeshGeometry.Components.GetUniqueName( "Point", false, 1 );
				point.Transform = new Transform( position, Quaternion.Identity );
				point.Enabled = true;
			}

			public Component_MeshGeometry_PolygonBasedPolyhedron MeshGeometry
			{
				get
				{
					if( CreatingObject != null )
					{
						var mesh = CreatingObject.GetComponent<Component_Mesh>();
						if( mesh != null )
							return mesh.GetComponent<Component_MeshGeometry_PolygonBasedPolyhedron>();
					}
					return null;
				}
			}

			public new Component_ObjectInSpace CreatingObject
			{
				get { return (Component_ObjectInSpace)base.CreatingObject; }
			}

			protected virtual bool CalculatePointPosition( Viewport viewport, out Vector3 position, out Component_ObjectInSpace collidedWith )
			{
				if( !viewport.MouseRelativeMode )
				{
					var sceneDocumentWindow = DocumentWindow as Component_Scene_DocumentWindow;
					if( sceneDocumentWindow != null )
					{
						var result = sceneDocumentWindow.CalculateCreateObjectPositionUnderCursor( viewport );
						if( result.found )
						{
							position = result.position;
							collidedWith = result.collidedWith;
							return true;
						}
					}
				}

				position = Vector3.Zero;
				collidedWith = null;
				return false;
			}

			void HeightStageStart( Viewport viewport )
			{
				heightStage = true;
				if( CalculatePointPosition( viewport, out var position, out _ ) )
					heightStagePosition = position;
			}

			protected override bool OnMouseDown( Viewport viewport, EMouseButtons button )
			{
				if( button == EMouseButtons.Left )
				{
					bool overStartPoint = !heightStage && lastStartPointRectangle.HasValue && lastStartPointRectangle.Value.Contains( viewport.MousePosition );

					if( heightStage )
					{
						Finish( false );
						return true;
					}
					else if( overStartPoint )
					{
						HeightStageStart( viewport );
						return true;
					}
					else
					{
						var points = MeshGeometry.GetPoints();

						if( !viewport.MouseRelativeMode )
						{
							if( points.Length >= 3 )
							{
								var plane = MeshGeometry.GetPolygonPlaneByPoints();
								var ray = viewport.CameraSettings.GetRayByScreenCoordinates( viewport.MousePosition );

								if( plane.Intersects( ray, out double scale ) )
								{
									var position = ray.GetPointOnRay( scale );

									var point = MeshGeometry.CreateComponent<Component_MeshGeometry_PolygonBasedPolyhedron_Point>( enabled: false );
									point.Name = MeshGeometry.Components.GetUniqueName( "Point", false, 1 );
									point.Transform = new Transform( position, Quaternion.Identity );
									point.Enabled = true;

									return true;
								}
							}
							else
							{
								if( CalculatePointPosition( viewport, out var position, out var collidedWith ) )
								{
									var point = MeshGeometry.CreateComponent<Component_MeshGeometry_PolygonBasedPolyhedron_Point>( enabled: false );
									point.Name = MeshGeometry.Components.GetUniqueName( "Point", false, 1 );
									point.Transform = new Transform( position, Quaternion.Identity );
									point.Enabled = true;

									//detect Clockwise
									var points2 = MeshGeometry.GetPointPositions();
									if( points2.Length == 3 )
									{
										var normal = Plane.FromPoints( points2[ 0 ], points2[ 1 ], points2[ 2 ] ).Normal;

										var d1 = ( points2[ 0 ] - viewport.CameraSettings.Position ).Length();
										var d2 = ( ( points2[ 0 ] + normal ) - viewport.CameraSettings.Position ).Length();

										if( d1 < d2 )
											MeshGeometry.Clockwise = true;
									}

									return true;
								}
							}
						}
					}
				}

				return false;
			}

			protected override void OnMouseMove( Viewport viewport, Vector2 mouse )
			{
				base.OnMouseMove( viewport, mouse );

				if( heightStage )
				{
					var ray = viewport.CameraSettings.GetRayByScreenCoordinates( mouse );

					var distance = ( MathAlgorithms.ProjectPointToLine( ray.Origin, ray.GetEndPoint(), heightStagePosition ) - heightStagePosition ).Length();

					MeshGeometry.Height = distance;
				}
			}

			protected override bool OnKeyDown( Viewport viewport, KeyEvent e )
			{
				if( e.Key == EKeys.Space || e.Key == EKeys.Return )
				{
					if( !heightStage )
						HeightStageStart( viewport );
					else
						Finish( false );
					return true;
				}

				if( e.Key == EKeys.Escape )
				{
					Finish( true );
					return true;
				}

				return false;
			}

			protected override void OnUpdateBeforeOutput( Viewport viewport )
			{
				base.OnUpdateBeforeOutput( viewport );

				lastStartPointRectangle = null;

				var points = MeshGeometry.GetPointPositions();
				if( !heightStage && points.Length > 2 )
				{
					if( viewport.CameraSettings.ProjectToScreenCoordinates( points[ 0 ], out var screenPosition ) )
					{
						var pos = points[ 0 ];

						Vector2 maxSize = new Vector2( 20, 20 );
						Vector2 minSize = new Vector2( 5, 5 );
						double maxDistance = 100;

						double distance = ( pos - viewport.CameraSettings.Position ).Length();
						if( distance < maxDistance )
						{
							Vector2 sizeInPixels = Vector2.Lerp( maxSize, minSize, distance / maxDistance );
							Vector2 screenSize = sizeInPixels / viewport.SizeInPixels.ToVector2();
							screenSize *= 1.5;

							var rect = new Rectangle( screenPosition - screenSize * .5, screenPosition + screenSize * .5 );

							ColorValue color;
							if( !viewport.MouseRelativeMode && rect.Contains( viewport.MousePosition ) )
								color = new ColorValue( 1, 1, 0, 0.5 );
							else
								color = new ColorValue( 1, 1, 1, 0.3 );

							viewport.CanvasRenderer.AddQuad( rect, color );

							lastStartPointRectangle = rect;
						}
					}
				}
			}

			protected override void OnGetTextInfoRightBottomCorner( List<string> lines )
			{
				base.OnGetTextInfoRightBottomCorner( lines );

				if( heightStage )
				{
					lines.Add( "Specify height of the object." );
					lines.Add( "Press Space, Return or click mouse button to finish creation." );
				}
				else
				{
					lines.Add( "Specify points of the object." );
					lines.Add( "Press Space or Return to finish creation of the points." );
				}
			}

			public override void Finish( bool cancel )
			{
				if( !cancel )
				{
					//calculate mesh in space position
					var points = MeshGeometry.GetPointPositions();
					if( points.Length != 0 )
					{
						var position = Vector3.Zero;
						foreach( var point in points )
							position += point;
						position /= points.Length;
						CreatingObject.Transform = new Transform( position, Quaternion.Identity );
					}

					//attach points to the mesh in space
					foreach( var point in MeshGeometry.GetPoints() )
						Component_ObjectInSpace_Utility.Attach( CreatingObject, point );

					//select meshin space and points
					var toSelect = new List<Component>();
					toSelect.Add( CreatingObject );
					//toSelect.AddRange( points );
					EditorAPI.SelectComponentsInMainObjectsWindow( DocumentWindow, toSelect.ToArray() );

					//update mesh
					MeshGeometry?.ShouldRecompileMesh();
				}

				base.Finish( cancel );
			}
		}

		/////////////////////////////////////////

		public Component_MeshGeometry_PolygonBasedPolyhedron_Point[] GetPoints()
		{
			return GetComponents<Component_MeshGeometry_PolygonBasedPolyhedron_Point>();
		}

		public Vector3[] GetPointPositions()
		{
			var points = GetPoints();
			var result = new Vector3[ points.Length ];
			for( int n = 0; n < points.Length; n++ )
				result[ n ] = points[ n ].TransformV.Position;
			return result;
		}

		public Plane GetPolygonPlaneByPoints()
		{
			var points = GetPointPositions();
			if( points.Length >= 3 )
				return Plane.FromPoints( points[ 0 ], points[ 1 ], points[ 2 ] );
			else
				return Plane.FromPointAndNormal( Vector3.Zero, Vector3.ZAxis );
		}

		public override void GetProceduralGeneratedData( ref VertexElement[] vertexStructure, ref byte[] vertices, ref int[] indices, ref Component_Material material, ref Component_Mesh.StructureClass structure )
		{
			var meshInSpace = Parent?.Parent as Component_MeshInSpace;
			var points = GetPointPositions();

			if( meshInSpace != null && points.Length >= 3 )
			{
				vertexStructure = StandardVertex.MakeStructure( StandardVertex.Components.StaticOneTexCoord, true, out int vertexSize );
				unsafe
				{
					if( vertexSize != sizeof( StandardVertex.StaticOneTexCoord ) )
						Log.Fatal( "vertexSize != sizeof( StandardVertexF )" );
				}

				SimpleMeshGenerator.GeneratePolygonBasedPolyhedron( points, Clockwise, Height, InsideOut, out Vector3[] positions, out Vector3[] normals, out Vector4[] tangents, out Vector2[] texCoords, out indices, out var faces );

				if( faces != null )
					structure = SimpleMeshGenerator.CreateMeshStructure( faces );

				var transformInverted = meshInSpace.TransformV.ToMatrix4().GetInverse();
				var rotationInverted = meshInSpace.TransformV.Rotation.GetInverse().ToMatrix3();

				vertices = new byte[ vertexSize * positions.Length ];
				unsafe
				{
					fixed ( byte* pVertices = vertices )
					{
						StandardVertex.StaticOneTexCoord* pVertex = (StandardVertex.StaticOneTexCoord*)pVertices;

						for( int n = 0; n < positions.Length; n++ )
						{
							pVertex->Position = ( transformInverted * positions[ n ] ).ToVector3F();
							pVertex->Normal = ( rotationInverted * normals[ n ] ).ToVector3F().GetNormalize();
							pVertex->Tangent = tangents[ n ].ToVector4F();
							pVertex->Color = new ColorValue( 1, 1, 1, 1 );
							pVertex->TexCoord0 = texCoords[ n ].ToVector2F();

							pVertex++;
						}
					}
				}
			}
		}

		protected override void OnComponentAdded( Component component )
		{
			base.OnComponentAdded( component );

			if( component is Component_MeshGeometry_PolygonBasedPolyhedron_Point )
				ShouldRecompileMesh();
		}

		protected override void OnComponentRemoved( Component component )
		{
			base.OnComponentRemoved( component );

			if( component is Component_MeshGeometry_PolygonBasedPolyhedron_Point )
				ShouldRecompileMesh();
		}

		protected override void OnEnabledInHierarchyChanged()
		{
			base.OnEnabledInHierarchyChanged();

			var meshInSpace = Parent?.Parent as Component_MeshInSpace;
			if( meshInSpace != null )
			{
				if( EnabledInHierarchy )
					meshInSpace.TransformChanged += MeshInSpace_TransformChanged;
				else
					meshInSpace.TransformChanged -= MeshInSpace_TransformChanged;
			}
		}

		private void MeshInSpace_TransformChanged( Component_ObjectInSpace obj )
		{
			ShouldRecompileMesh();
		}
	}

	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	/// <summary>
	/// Represents a point of the <see cref="Component_MeshGeometry_PolygonBasedPolyhedron"/>.
	/// </summary>
	public class Component_MeshGeometry_PolygonBasedPolyhedron_Point : Component_ObjectInSpace
	{
		//!!!!good?
		bool lastDisableShowingLabelForThisObject;

		//

		public override void OnGetRenderSceneData( ViewportRenderingContext context, GetRenderSceneDataMode mode )
		{
			lastDisableShowingLabelForThisObject = false;

			//hide label
			var geometry = Parent as Component_MeshGeometry_PolygonBasedPolyhedron;
			if( geometry != null )
			{
				//context.objectInSpaceRenderingContext.objectToCreate

				if( !geometry.AlwaysDisplayPointLabels )
				{
					var context2 = context.objectInSpaceRenderingContext;

					var objectsToCheck = new List<object>();
					{
						objectsToCheck.Add( geometry );
						objectsToCheck.AddRange( geometry.GetPoints() );

						var mesh = geometry.Parent;
						if( mesh != null )
						{
							objectsToCheck.Add( mesh );

							var meshInSpace = mesh.Parent;
							if( meshInSpace != null )
								objectsToCheck.Add( meshInSpace );
						}
					}

					bool display = false;
					foreach( var obj in objectsToCheck )
					{
						display = context2.selectedObjects.Contains( obj ) || context2.objectToCreate == obj;
						if( display )
							break;
					}

					if( !display )
					{
						context2.disableShowingLabelForThisObject = true;
						lastDisableShowingLabelForThisObject = true;
					}
				}
			}
		}

		protected override bool OnEnabledSelectionByCursor()
		{
			if( !ParentScene.GetDisplayDevelopmentDataInThisApplication() || !ParentScene.DisplayLabels )
				return false;
			if( lastDisableShowingLabelForThisObject )
				return false;
			return base.OnEnabledSelectionByCursor();
		}

		protected override void OnTransformChanged()
		{
			base.OnTransformChanged();

			var geometry = Parent as Component_MeshGeometry_PolygonBasedPolyhedron;
			geometry?.ShouldRecompileMesh();
		}
	}

}