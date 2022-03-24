# Workload Manager
This is a simple tool that can be used to help manager large workloads composed of many steps.
Important concepts are:

## WorkloadManager
The workload manager handle a queue of work items.

## WorkloadRunner
A workload runner is responsible for running the items contained in a workload manager.

## WorkItem
A unit of work of the entire workload.  This can be any type defined by the user.
