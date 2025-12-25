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
        /// <summary>
        /// Invoke a CLR instance method via reflection.
        /// Parameters are taken from the parameter stack.
        /// </summary>
        MIV,
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
