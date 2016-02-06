using UnityEngine;
using System.Collections;
using System;

namespace Vox {

	public class GenMeshJob : VoxelJob {

		public uint xOff, yOff, zOff;
		public byte detailLevel;
		private VoxelBlock block;
		private Tree control;

		public GenMeshJob(VoxelBlock block, Tree control, byte detailLevel) {
			this.block = block;
			this.control = control;
			this.detailLevel = detailLevel;
		}

		public override void execute() {
			lock (control) {
				System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
				watch.Start();

				Voxel[,,] array = control.getArray(
					xOff *VoxelRenderer.VOXEL_DIMENSION,
					yOff *VoxelRenderer.VOXEL_DIMENSION,
					zOff *VoxelRenderer.VOXEL_DIMENSION,
					(xOff +1) *VoxelRenderer.VOXEL_DIMENSION +1,
					(yOff +1) *VoxelRenderer.VOXEL_DIMENSION +1,
					(zOff +1) *VoxelRenderer.VOXEL_DIMENSION +1);
				getRenderer().genMesh(array);

				watch.Stop();

				control.meshGenTime += watch.Elapsed.TotalSeconds;
				++control.meshGenCount;
			}
		}

		public VoxelBlock getBlock() {
			return block;
		}

		public VoxelRenderer getRenderer() {
			Index i = new Index(detailLevel, xOff, yOff, zOff);
			return control.getRenderer(i);
        }

		public void setOffset(uint xOff, uint yOff, uint zOff) {
			this.xOff = xOff;
			this.yOff = yOff;
			this.zOff = zOff;
		}
	}

	public class ApplyMeshJob : VoxelJob {

		private VoxelRenderer rend;
		private byte detailLevel;
		private uint x, y, z;

		public ApplyMeshJob(VoxelRenderer rend, byte detailLevel, uint x, uint y, uint z) {
			//MonoBehaviour.print("CREATING!");
			this.rend = rend;
			this.detailLevel = detailLevel;
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public override void execute() {
			//MonoBehaviour.print("APPLYING!");
			System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
			watch.Start();
			Vector3 pos = rend.position / rend.size;
			if (VoxelBlock.isRenderLod(pos.x, pos.y, pos.z, rend.size, rend.control) || VoxelBlock.isRenderSize(rend.size, rend.control))
				rend.applyMesh();
			watch.Stop();
			lock(rend.control) {
				rend.control.meshApplyTime += watch.Elapsed.TotalSeconds;
				++rend.control.meshApplyCount;
			}
		}
	}

	public class UpdateCheckJob : VoxelJob {

		public byte xOff, yOff, zOff;
		public byte detailLevel;
		private VoxelBlock block;
		private Tree control;
		
		public UpdateCheckJob(VoxelBlock block, Tree control, byte detailLevel) {
			this.block = block;
			this.control = control;
			this.detailLevel = detailLevel;
			control.addUpdateCheckJob();
		}

		public override void execute() {
			lock (control) {
				block.updateAll(xOff, yOff, zOff, detailLevel, control);
				control.removeUpdateCheckJob();
			}
		}

		public void setOffset(byte xOff, byte yOff, byte zOff) {
			this.xOff = xOff;
			this.yOff = yOff;
			this.zOff = zOff;
		}
	}

	public class DropRendererJob : VoxelJob {

		private VoxelRenderer rend;
		
		public DropRendererJob(VoxelRenderer rend) {
			this.rend = rend;
		}

		public override void execute() {
			lock (rend.control) {
				rend.clear();
			}
		}
	}

	public class LinkRenderersJob: VoxelJob {
		private Tree control;

		public LinkRenderersJob(Tree control) {
			this.control = control;
		}

		public override void execute() {
			control.relinkRenderers();
		}
	}

}
