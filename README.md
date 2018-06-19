# NPhilosophers
College project (Advanced Operating Systems) - C# implementation of process synchronization algorithms (2017/2018)

# Setup
N philosophers have gathered on some conference. In contrast to the famous problem of 5 philosphers, here we have room for only one person at the table, and not for 5. Hence, the access to the table is **critical section** because **only one** philosopher can at any time be at the table, or none. At the start, the main process creates N philosopher processes (number N>2 is input). Process communication is done using **pipelines** (either unnamed or named).

```
process philosopher{
   take part in conference; // sleep(1);
   access table, eat and write "Philosopher i is at the table";    // sleep(3); (critical section)
   take part in conference; // sleep(1);
}
```

N philosopher processes are syncronized using **Ricart-Agrawala Algorithm**

All processes write down the message they send and message they receive.
