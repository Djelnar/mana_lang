#pragma once
#define DEBUG 1



#if defined(ARDUINO)
#define AVR_PLATFORM
#endif

#if defined(WIN32)
#include <codecvt>
#include <functional>
#include <locale>
#elif defined(__linux__)
#include <functional>
#endif

#if defined(AVR_PLATFORM)
#include "Arduino.hpp"
#define ASM(x) __ASM volatile (x)
#define sleep(x) delay(x)
#else
#include <string>
typedef uint8_t byte;
typedef std::string String;
#define ASM(x)
#define sleep(x) 
void setup(int argc, char* argv[]);
void loop();
#endif

typedef const char* nativeString;
typedef void* wpointer;
typedef unsigned char uchar_t;
typedef unsigned char byte;

static inline wpointer malloc0(const size_t x)
{
    if (x)
        return calloc(1, x);
    return nullptr;
}

#define ZERO_ARRAY 1

#define new0(type,size)  ((type *) malloc0(sizeof (type) * (size)))

#define READ32(x) (*(x))
#define READ64(x) ((uint64_t)(*(x+1)) << 32 | \
                   (uint64_t)(*(x)))

#define PTR_TO_INT(x) static_cast<int32_t>(reinterpret_cast<intptr_t>(x)) 
#define INT_TO_PTR(x) reinterpret_cast<wpointer>(static_cast<intptr_t>(x))

#define LAMBDA_TRUE(_) [](_) { return true; }
#define LAMBDA_FALSE(_) [](_) { return false; }

#ifdef DEBUG
#define d_print(x) Serial.print(x)
#define f_print(x) do {  Serial.print(#x);Serial.print(" ");Serial.println(x); } while(0)
#define w_print(x) Serial.println(x)
#define init_serial() Serial.begin(9600)

#ifndef AVR_PLATFORM
#include <iostream>
#undef d_print
#undef f_print
#undef w_print
#undef init_serial
#define init_serial()
#define d_print(x) std::cout << x
#define f_print(x) std::cout << #x << " " << x << "\n"
#define w_print(x) std::cout << x << "\n"
#endif

#else
#define d_print(x)
#define f_print(x)
#define w_print(x)
#define init_serial()
#endif


#if defined(__GNUC__)
#define _NODISCARD [[nodiscard]]
#endif

static std::wstring BytesToUTF8(const char* in, size_t size)
{
    std::wstring out;
    unsigned int codepoint;
    int arrow = 0;
    while (arrow != size)
    {
        unsigned char ch = static_cast<unsigned char>(*in);
        if (ch <= 0x7f)
            codepoint = ch;
        else if (ch <= 0xbf)
            codepoint = (codepoint << 6) | (ch & 0x3f);
        else if (ch <= 0xdf)
            codepoint = ch & 0x1f;
        else if (ch <= 0xef)
            codepoint = ch & 0x0f;
        else
            codepoint = ch & 0x07;
        ++in;
        ++arrow;
        if (((*in & 0xc0) != 0x80) && (codepoint <= 0x10ffff))
        {
            if (sizeof(wchar_t) > 2)
                out.append(1, static_cast<wchar_t>(codepoint));
            else if (codepoint > 0xffff)
            {
                out.append(1, static_cast<wchar_t>(0xd800 + (codepoint >> 10)));
                out.append(1, static_cast<wchar_t>(0xdc00 + (codepoint & 0x03ff)));
            }
            else if (codepoint < 0xd800 || codepoint >= 0xe000)
                out.append(1, static_cast<wchar_t>(codepoint));
        }
    }
    return out;
}

#define CUSTOM_EXCEPTION(name) struct name : public std::exception {   \
    const char* msg;                                                   \
    name() { msg = ""; }                                               \
    name(const char* message) { msg = (message); }                     \
    name(std::string message) { msg = (message.c_str()); }             \
    _NODISCARD const char* what() const throw () { return msg; }       \
}
#define CUSTOM_WEXCEPTION(name) struct name                        {   \
    const wchar_t* msg;                                                \
    name() { msg = L""; }                                              \
    name(const wchar_t* message) { msg = (message); }                  \
    name(std::wstring message  ) { msg = (message.c_str()); }          \
    _NODISCARD const wchar_t* what() const throw () { return msg; }    \
}


template<typename T>
using Comparer = int(T t1, T t2);

template<typename T>
using Predicate = std::function<bool(T z)>;

template<typename T>
using Action0 = void(T t);
template<typename TSelf, typename T1>
using Action1 = void(TSelf self, T1 t1);
template<typename TSelf, typename T1, typename T2>
using Action2 = void(TSelf self, T1 t1, T2 t2);

template<typename T>
using Func0 = T();
template<typename T0, typename T1>
using Func1 = T0(T1 arg1);



using GetConstByIndexDelegate = std::function<std::wstring(int z)>;

#if __cplusplus >= 201406
template<typename T>
struct Nullable { inline static const T Value = NULL; };

template<typename T>
 struct Nullable<T*> { inline static const T* Value = nullptr; };
#else
template<typename T>
struct Nullable { static const T Value = NULL; };

template<typename T>
struct Nullable<T*>
{
    inline static T* Value = nullptr;
};
#endif

#include "api/memory_barrier.hpp"

#define NULL_VALUE(T) Nullable<T>::Value

#if defined(AVR_PLATFORM)
namespace std
{
    template<class InputIt, class OutputIt>
    OutputIt copy(InputIt first, InputIt last, OutputIt d_first)
    {
        while (first != last)
            *d_first++ = *first++;
        return d_first;
    }

}
#endif
template<typename T>
void array_copy(T* sourceArray, int sourceIndex, T* destinationArray, int destinationIndex, int length)
{
    std::copy(sourceArray + sourceIndex,
        sourceArray + sourceIndex + length,
        destinationArray + destinationIndex);
}
inline void vm_shutdown()
{
    w_print("\t !! VM SHUTDOWN !!");
    while (true)
    {
        sleep(200);
    }
}


#if __cplusplus >= 201703L
#define REGISTER 
#else
#define REGISTER register 
#endif
static const char* out_of_memory_Str = "<<OUT OF MEMORY>>";
inline void throw_out_of_memory()
{
    w_print(out_of_memory_Str);
    vm_shutdown();
}


#if defined(AVR_PLATFORM)
#ifdef __arm__
extern "C" char* sbrk(int incr);
#else
extern char* __brkval;
#endif 

int freeMemory() {
    char top;
#ifdef __arm__
    return &top - reinterpret_cast<char*>(sbrk(0));
#elif defined(CORE_TEENSY) || (ARDUINO > 103 && ARDUINO != 151)
    return &top - __brkval;
#else
    return __brkval ? &top - __brkval : &top - __malloc_heap_start;
#endif
}
#endif
#if defined(AVR_PLATFORM)
#define MEM_CHECK(predicate) \
    if ((freeMemory() <= 1024)) { throw_out_of_memory(); }
#else
#define MEM_CHECK(predicate) \
    if (predicate) { throw_out_of_memory(); }
#endif


