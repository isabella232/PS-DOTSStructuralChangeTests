using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Entities
{
    public struct CustomEntityManagerCommand
    {
        public Entity parent;
        public Entity oldChildToDestroy;
        public Entity prefabToInstantiateNewChildFrom;
        public float4x4 newChildToParentTransform;

        public ComponentType parentToChildReferenceType;
        public ComponentType childToParentReferenceType;
        public ComponentType childToParentTransformType;
    }

    public unsafe partial struct EntityManager
    {
        public void CustomExtensionInit()
        {
            StructuralChangeExtension.Init();
        }

        [StructuralChangeMethod]
        public void DoCustomCommand(in CustomEntityManagerCommand customCommand)
        {
            var access = GetCheckedEntityDataAccess();
            access->DoCustomCommand(customCommand);
        }

        [StructuralChangeMethod]
        public void DoCustomCommands(in NativeArray<CustomEntityManagerCommand> customCommand)
        {
            var access = GetCheckedEntityDataAccess();
            access->DoCustomCommands(customCommand);
        }

        [StructuralChangeMethod]
        public void DoCustomCommands_Batched(in NativeArray<CustomEntityManagerCommand> customCommand)
        {
            var access = GetCheckedEntityDataAccess();
            access->DoCustomCommands_Batched(customCommand);
        }
    }

    [BurstCompile]
    public unsafe partial struct StructuralChangeExtension
    {
        public delegate void del_DoCustomCommand(IntPtr entityComponentStore, IntPtr customeCommand);
        public static del_DoCustomCommand _del_DoCustomCommand;

        public delegate void del_DoCustomCommands(IntPtr entityComponentStore, IntPtr customeCommands, int count);
        public static del_DoCustomCommands _del_DoCustomCommands;
        public static del_DoCustomCommands _del_DoCustomCommands_Batched;

        public static void Init()
        {
            _del_DoCustomCommand = BurstCompiler.CompileFunctionPointer<del_DoCustomCommand>(_mono_to_burst_DoCustomCommand).Invoke;
            _del_DoCustomCommands = BurstCompiler.CompileFunctionPointer<del_DoCustomCommands>(_mono_to_burst_DoCustomCommands).Invoke;
            _del_DoCustomCommands_Batched = BurstCompiler.CompileFunctionPointer<del_DoCustomCommands>(_mono_to_burst_DoCustomCommands_Batched).Invoke;
        }

        internal static void DoCustomCommand(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommand)
        {
            _forward_mono_DoCustomCommand(entityComponentStore, customCommand);
        }

        static void _forward_mono_DoCustomCommand(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommand)
        {
            _del_DoCustomCommand((IntPtr)entityComponentStore, (IntPtr)customCommand);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(del_DoCustomCommand))]
        static void _mono_to_burst_DoCustomCommand(IntPtr entityComponentStore, IntPtr customCommand)
        {
            _DoCustomCommand((EntityComponentStore*)entityComponentStore, (CustomEntityManagerCommand*)customCommand);
        }

        static void _DoCustomCommand(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommand)
        {
            if (customCommand->oldChildToDestroy != Entity.Null)
                entityComponentStore->DestroyEntityWithValidation(customCommand->oldChildToDestroy);

            Entity newEntity;
            entityComponentStore->InstantiateEntities(customCommand->prefabToInstantiateNewChildFrom, &newEntity, 1);

            entityComponentStore->AddComponent(newEntity, customCommand->childToParentReferenceType);
            entityComponentStore->AddComponent(newEntity, customCommand->childToParentTransformType);

            var parent = customCommand->parent;
            var toParentTransform = customCommand->newChildToParentTransform;

            var typeIndex = customCommand->childToParentReferenceType.TypeIndex;
            var ptr = entityComponentStore->GetComponentDataWithTypeRW(newEntity, typeIndex, entityComponentStore->GlobalSystemVersion);
            UnsafeUtility.CopyStructureToPtr(ref parent, ptr);

            typeIndex = customCommand->childToParentTransformType.TypeIndex;
            ptr = entityComponentStore->GetComponentDataWithTypeRW(newEntity, typeIndex, entityComponentStore->GlobalSystemVersion);
            UnsafeUtility.CopyStructureToPtr(ref toParentTransform, ptr);

            typeIndex = customCommand->parentToChildReferenceType.TypeIndex;
            ptr = entityComponentStore->GetComponentDataWithTypeRW(parent, typeIndex, entityComponentStore->GlobalSystemVersion);
            UnsafeUtility.CopyStructureToPtr(ref newEntity, ptr);
        }

        internal static void DoCustomCommands(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommands, int count)
        {
            _forward_mono_DoCustomCommands(entityComponentStore, customCommands, count);
        }

        static void _forward_mono_DoCustomCommands(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommands, int count)
        {
            _del_DoCustomCommands((IntPtr)entityComponentStore, (IntPtr)customCommands, count);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(del_DoCustomCommand))]
        static void _mono_to_burst_DoCustomCommands(IntPtr entityComponentStore, IntPtr customCommands, int count)
        {
            _DoCustomCommands((EntityComponentStore*)entityComponentStore, (CustomEntityManagerCommand*)customCommands, count);
        }

        static void _DoCustomCommands(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommands, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                var customCommand = customCommands + i;

                _DoCustomCommand(entityComponentStore, customCommand);
            }
        }

        internal static void DoCustomCommands_Batched(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommands, int count)
        {
            _forward_mono_DoCustomCommands_Batched(entityComponentStore, customCommands, count);
        }

        static void _forward_mono_DoCustomCommands_Batched(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommands, int count)
        {
            _del_DoCustomCommands_Batched((IntPtr)entityComponentStore, (IntPtr)customCommands, count);
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(del_DoCustomCommand))]
        static void _mono_to_burst_DoCustomCommands_Batched(IntPtr entityComponentStore, IntPtr customCommands, int count)
        {
            _DoCustomCommands_Batched((EntityComponentStore*)entityComponentStore, (CustomEntityManagerCommand*)customCommands, count);
        }

        static void _DoCustomCommands_Batched(EntityComponentStore* entityComponentStore, CustomEntityManagerCommand* customCommands, int count)
        {
            if (count <= 0)
                return;

            // Entity cache contains entities to be destroyed
            var entityCache = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < count; ++i)
            {
                var customCommand = customCommands + i;

                if (customCommand->oldChildToDestroy != Entity.Null)
                    entityCache.Add(customCommand->oldChildToDestroy);
            }

            entityComponentStore->DestroyEntities((Entity*)entityCache.GetUnsafeReadOnlyPtr(), entityCache.Length);

            // Entity cache contains entities just instantiated
            entityCache.Clear();
            for (int i = 0; i < count; ++i)
            {
                var customCommand = customCommands + i;

                Entity newEntity;
                entityComponentStore->InstantiateEntities(customCommand->prefabToInstantiateNewChildFrom, &newEntity, 1);
                entityCache.Add(newEntity);
            }

            // Add component for new entities in batch
            var entityBatchList = new NativeList<EntityBatchInChunk>(Allocator.Temp);
            {
                var childToParentReferenceType = customCommands[0].childToParentReferenceType;
                entityComponentStore->CreateEntityBatchListForAddComponent(entityCache, childToParentReferenceType, out entityBatchList);
                entityComponentStore->AddComponent((UnsafeList*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref entityBatchList), childToParentReferenceType, 0);

                entityBatchList.Clear();

                var childToParentTransformType = customCommands[0].childToParentTransformType;
                entityComponentStore->CreateEntityBatchListForAddComponent(entityCache, childToParentTransformType, out entityBatchList);
                entityComponentStore->AddComponent((UnsafeList*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref entityBatchList), childToParentTransformType, 0);
            }
            entityBatchList.Dispose();

            // Set component data for new entities
            for (int i = 0; i < count; ++i)
            {
                var customCommand = customCommands + i;
                Entity newEntity = entityCache[i];
                
                var parent = customCommand->parent;
                var toParentTransform = customCommand->newChildToParentTransform;

                var typeIndex = customCommand->childToParentReferenceType.TypeIndex;
                var ptr = entityComponentStore->GetComponentDataWithTypeRW(newEntity, typeIndex, entityComponentStore->GlobalSystemVersion);
                UnsafeUtility.CopyStructureToPtr(ref parent, ptr);

                typeIndex = customCommand->childToParentTransformType.TypeIndex;
                ptr = entityComponentStore->GetComponentDataWithTypeRW(newEntity, typeIndex, entityComponentStore->GlobalSystemVersion);
                UnsafeUtility.CopyStructureToPtr(ref toParentTransform, ptr);

                typeIndex = customCommand->parentToChildReferenceType.TypeIndex;
                ptr = entityComponentStore->GetComponentDataWithTypeRW(parent, typeIndex, entityComponentStore->GlobalSystemVersion);
                UnsafeUtility.CopyStructureToPtr(ref newEntity, ptr);
            }

            entityCache.Dispose();
        }
    }

    unsafe partial struct EntityDataAccess : IDisposable
    {
        public void DoCustomCommand(CustomEntityManagerCommand customCommand)
        {
            BeforeStructuralChange();

            var archetypeChanges = EntityComponentStore->BeginArchetypeChangeTracking();

            StructuralChangeExtension.DoCustomCommand(EntityComponentStore, &customCommand);

            EntityComponentStore->EndArchetypeChangeTracking(archetypeChanges, EntityQueryManager);
            EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
            PlaybackManagedChanges();
        }

        public void DoCustomCommands(in NativeArray<CustomEntityManagerCommand> customCommands)
        {
            BeforeStructuralChange();

            var archetypeChanges = EntityComponentStore->BeginArchetypeChangeTracking();

            StructuralChangeExtension.DoCustomCommands(EntityComponentStore, (CustomEntityManagerCommand*)customCommands.GetUnsafeReadOnlyPtr(), customCommands.Length);

            EntityComponentStore->EndArchetypeChangeTracking(archetypeChanges, EntityQueryManager);
            EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
            PlaybackManagedChanges();
        }

        public void DoCustomCommands_Batched(in NativeArray<CustomEntityManagerCommand> customCommands)
        {
            BeforeStructuralChange();

            var archetypeChanges = EntityComponentStore->BeginArchetypeChangeTracking();

            StructuralChangeExtension.DoCustomCommands_Batched(EntityComponentStore, (CustomEntityManagerCommand*)customCommands.GetUnsafeReadOnlyPtr(), customCommands.Length);

            EntityComponentStore->EndArchetypeChangeTracking(archetypeChanges, EntityQueryManager);
            EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
            PlaybackManagedChanges();
        }
    }
}
