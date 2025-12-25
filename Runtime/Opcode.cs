using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{

    public enum OpCode
    {
        DBG,
        NOP,
        DSB,
        DB,
        INT,      
        MOV,
        INC,
        DEC,
        NEG,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        TEST,
        CEQ,
        CNE,
        CG,
        CGE,
        CL,
        CLE,
        OR,
        AND,
        NOT,
        JMP,
        JZ,
        JNZ,
        DC,
        PTR,
        PUSH,
        POP,
        CALL,
        RET,
        INV,
        RUN,
        SHL,
        SHR,
        ANDB, 
        ORB, 
        NOTB,
        XOR,
        DCO, 
        LOCK,
        FREE,
    }

}
