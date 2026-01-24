<template>
  <div>
    <button @click="test(CONSTANT)">Click</button>
  </div>
</template>

<script setup>
const CONSTANT = '{{ test }}'
function test(val) { console.log(val) }
</script>
